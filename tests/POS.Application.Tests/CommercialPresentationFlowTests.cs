using System.Reflection;
using POS.Application.DTOs.Invoices;
using POS.Application.DTOs.Quotes;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Invoices;
using POS.Application.UseCases.Quotes;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests;

public sealed class CommercialPresentationFlowTests
{
    [Fact]
    public async Task ConvertQuoteToSale_PreservesQuotedBoxPriceAndEquivalence_WhenCatalogChanges()
    {
        var product = MakeBoxProduct(stock: 24m, unitsPerBox: 10, boxPrice: 95_000m);
        var quote = new Quote("COT-BOX-001", "Ana Gomez", 30);
        quote.AddItem(new QuoteItem(
            product.Id,
            product.Name,
            new Money(110_000m),
            new Quantity(1m),
            10m,
            ProductPresentation.Box,
            12));

        var products = new SingleProductRepository(product);
        var sales = new SaleRepository();
        var unitOfWork = new TrackingUnitOfWork();
        var createSale = new CreateSaleUseCase(products, sales, unitOfWork, authorizer: TestAuthorizer.Create());
        var quotes = new SingleQuoteRepository(quote);
        var useCase = new ConvertQuoteToSaleUseCase(quotes, createSale, unitOfWork);

        await useCase.ExecuteAsync(
            new ConvertQuoteToSaleRequest(
                quote.Id,
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 99_000m, 99_000m, null)]),
            TestContext.Current.CancellationToken);

        SaleItem line = Assert.Single(Assert.Single(sales.Sales).Items);
        Assert.Equal(ProductPresentation.Box, line.Presentation);
        Assert.Equal(12, line.UnitsPerPresentation);
        Assert.Equal(110_000m, line.UnitPrice.Amount);
        Assert.Equal(99_000m, line.Subtotal().Amount);
        Assert.Equal(12m, product.CurrentStock);
        Assert.Equal(QuoteStatus.Converted, quote.Status);
        Assert.Equal(1, unitOfWork.TransactionCount);
    }

    [Fact]
    public async Task ProcessReturn_PartialBoxReturn_RestoresSnapshotBaseUnits()
    {
        var product = MakeBoxProduct(stock: 24m, unitsPerBox: 12, boxPrice: 110_000m);
        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(2m), ProductPresentation.Box, Money.Zero);
        sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(220_000m), new Money(220_000m)));
        sale.Complete();

        var administrator = new User("admin", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
        var inventory = new InventoryRepository();
        var returns = new SaleReturnRepository();
        var useCase = new ProcessReturnUseCase(
            new SaleRepository(sale),
            new SingleProductRepository(product),
            inventory,
            returns,
            new TrackingUnitOfWork(),
            new SingleUserRepository(administrator),
            receivables: new ReturnResolutionRepository(),
            authorizer: TestAuthorizer.Create());

        await useCase.ExecuteAsync(
            new ProcessReturnRequest(
                sale.Id,
                "Customer requested a return",
                Guid.NewGuid(),
                ReturnResolutionChoice.Cash,
                [new ReturnLineRequest(Assert.Single(sale.Items).Id, 1)]),
            TestContext.Current.CancellationToken);

        Assert.Equal(12m, product.CurrentStock);
        InventoryMovement movement = Assert.Single(inventory.Movements);
        Assert.Equal(12m, movement.Quantity);
        Assert.Equal(InventoryMovementType.Return, movement.Type);
        SaleReturn saleReturn = Assert.Single(returns.Returns);
        Assert.Equal(1, Assert.Single(saleReturn.Items).QuantityReturned);
    }

    [Fact]
    public async Task GetQuoteHistory_LabelsSuccessfulEmailedQuotesByCalendarExpiry()
    {
        Quote dueToday = MakeQuote("COT-DUE", DateTime.Now.Date);
        Quote expiringSoon = MakeQuote("COT-SOON", DateTime.Now.Date.AddDays(2));
        Quote expired = MakeQuote("COT-EXPIRED", DateTime.Now.Date.AddDays(-1));
        Quote notEmailed = MakeQuote("COT-NO-EMAIL", DateTime.Now.Date.AddDays(1));
        var quotes = new QuoteHistoryRepository([dueToday, expiringSoon, expired, notEmailed]);
        var logs = new DocumentSendLogRepository(
            [
                SuccessfulQuoteEmail(dueToday.Id),
                SuccessfulQuoteEmail(expiringSoon.Id),
                SuccessfulQuoteEmail(expired.Id),
            ]);
        var useCase = new GetQuoteHistoryUseCase(quotes, logs);

        QuoteHistoryResult result = await useCase.ExecuteAsync(null, null, null, null, 1, 10, TestContext.Current.CancellationToken);

        Assert.Equal("Vence hoy", result.Items.Single(item => item.QuoteId == dueToday.Id).ReminderLabel);
        Assert.Equal("Próxima a vencer", result.Items.Single(item => item.QuoteId == expiringSoon.Id).ReminderLabel);
        Assert.Equal("Vencida", result.Items.Single(item => item.QuoteId == expired.Id).ReminderLabel);
        Assert.Null(result.Items.Single(item => item.QuoteId == notEmailed.Id).ReminderLabel);
    }

    private static Product MakeBoxProduct(decimal stock, int unitsPerBox, decimal boxPrice) => new(
        "Ampolla 350",
        Guid.NewGuid(),
        new Money(6_000m),
        new Money(10_000m),
        new StockQuantity(stock),
        new StockQuantity(0m),
        unitsPerBox: unitsPerBox,
        boxPrice: new Money(boxPrice));

    private static Quote MakeQuote(string quoteNumber, DateTime expiresAt)
    {
        var quote = new Quote(quoteNumber, "Ana Gomez", 30);
        typeof(Quote)
            .GetProperty(nameof(Quote.ExpiresAt), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(quote, expiresAt);
        return quote;
    }

    private static DocumentSendLog SuccessfulQuoteEmail(Guid quoteId) =>
        new(DocumentType.Quote, quoteId, SendChannel.Email, "ana@example.com", success: true);

    private sealed class SingleProductRepository(Product product) : IProductRepository
    {
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == product.Id ? product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(null);

        public Task AddAsync(Product entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SaleRepository : ISaleRepository
    {
        public SaleRepository(params Sale[] sales) => Sales.AddRange(sales);

        public List<Sale> Sales { get; } = [];

        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
        {
            Sales.Add(sale);
            return Task.CompletedTask;
        }

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sales.SingleOrDefault(sale => sale.Id == id));

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class SingleQuoteRepository(Quote quote) : IQuoteRepository
    {
        public Task<Quote?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(id == quote.Id ? quote : null);

        public Task<IReadOnlyList<Quote>> GetOpenByCustomerIdAsync(Guid customerId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Quote>>([]);

        public Task<string> GetNextQuoteNumberAsync(CancellationToken ct = default) => Task.FromResult("COT-0001");

        public Task AddAsync(Quote entity, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Quote entity, CancellationToken ct = default) => Task.CompletedTask;

        public Task<(IReadOnlyList<Quote> Items, int Total)> GetHistoryAsync(
            DateOnly? from,
            DateOnly? to,
            string? customerName,
            QuoteStatus? status,
            int page,
            int pageSize,
            CancellationToken ct = default) =>
            Task.FromResult<(IReadOnlyList<Quote>, int)>(([quote], 1));
    }

    private sealed class QuoteHistoryRepository(IReadOnlyList<Quote> quotes) : IQuoteRepository
    {
        public Task<Quote?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(quotes.SingleOrDefault(quote => quote.Id == id));

        public Task<IReadOnlyList<Quote>> GetOpenByCustomerIdAsync(Guid customerId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Quote>>([]);

        public Task<string> GetNextQuoteNumberAsync(CancellationToken ct = default) => Task.FromResult("COT-0001");

        public Task AddAsync(Quote quote, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Quote quote, CancellationToken ct = default) => Task.CompletedTask;

        public Task<(IReadOnlyList<Quote> Items, int Total)> GetHistoryAsync(
            DateOnly? from,
            DateOnly? to,
            string? customerName,
            QuoteStatus? status,
            int page,
            int pageSize,
            CancellationToken ct = default) =>
            Task.FromResult((quotes, quotes.Count));
    }

    private sealed class TrackingUnitOfWork : IUnitOfWork
    {
        public int TransactionCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            TransactionCount++;
            await operation(cancellationToken);
        }
    }

    private sealed class InventoryRepository : IInventoryRepository
    {
        public List<InventoryMovement> Movements { get; } = [];

        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InventoryStockLevel>>([]);

        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<InventoryMovement>>(Movements.Where(movement => movement.ProductId == productId).ToList());

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Product>>([]);

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            Movements.Add(movement);
            return Task.CompletedTask;
        }
    }

    private sealed class SaleReturnRepository : ISaleReturnRepository
    {
        public List<SaleReturn> Returns { get; } = [];

        public Task AddAsync(SaleReturn saleReturn, CancellationToken cancellationToken = default)
        {
            Returns.Add(saleReturn);
            return Task.CompletedTask;
        }

        public Task<SaleReturn?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Returns.SingleOrDefault(saleReturn => saleReturn.OriginalSaleId == saleId));

        public Task<IReadOnlyList<SaleReturn>> GetAllBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SaleReturn>>(Returns.Where(saleReturn => saleReturn.OriginalSaleId == saleId).ToList());

        public Task<SaleReturn?> GetByOperationIdAsync(Guid operationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Returns.SingleOrDefault(saleReturn => saleReturn.OperationId == operationId));
    }

    private sealed class ReturnResolutionRepository : IReceivableRepository
    {
        public Task AddAsync(Receivable receivable, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Receivable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Receivable?>(null);
        public Task UpdateAsync(Receivable receivable, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddReturnSettlementAsync(ReturnSettlement settlement, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddReturnReceivableReconciliationAsync(ReturnReceivableReconciliation reconciliation, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SingleUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == user.Id ? user : null);

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(null);

        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task AddAsync(User entity, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<User>>([user]);

        public Task UpdateAsync(User entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(user.RoleId == roleId && user.IsActive ? 1 : 0);
    }

    private sealed class AdministratorRoleRepository : IRoleRepository
    {
        private static readonly Role Administrator = Role.CreateSystemRole(POS.Domain.Authorization.RoleConstants.AdministratorRoleId, POS.Domain.Authorization.RoleConstants.AdministratorRoleName);
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Role?>(id == Administrator.Id ? Administrator : null);
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Role?>(null);
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>([Administrator]);
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task AddAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class DocumentSendLogRepository(IReadOnlyList<DocumentSendLog> logs) : IDocumentSendLogRepository
    {
        public Task AddAsync(DocumentSendLog log, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<DocumentSendLog>> GetByDocumentAsync(Guid documentId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentSendLog>>(logs.Where(log => log.DocumentId == documentId).ToList());

        public Task<DocumentSendLog?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(logs.SingleOrDefault(log => log.Id == id));
    }
}
