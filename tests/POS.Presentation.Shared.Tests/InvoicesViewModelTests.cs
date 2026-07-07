using POS.Application.DTOs.Invoices;
using POS.Application.Interfaces;
using POS.Application.UseCases.Invoices;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.Invoices;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class InvoicesViewModelTests
{
    [Fact]
    public async Task ExportPdfAsync_WhenInvoiceIsSelected_RaisesFileExportWithInvoicePdf()
    {
        var saleId = Guid.NewGuid();
        var pdfService = new FakePdfService([1, 2, 3, 4]);
        var viewModel = new InvoicesViewModel(
            new QuerySalesHistoryUseCase(new StaticSalesHistoryQueryService(new SaleHistoryResult(
                saleId,
                "FAC/001",
                null,
                null,
                SaleStatus.Completed,
                DateTimeOffset.Now,
                15_000m))),
            new ReprintReceiptUseCase(new EmptySaleRepository(), new NoOpPrintService()),
            new ProcessReturnUseCase(
                new EmptySaleRepository(),
                new EmptyProductRepository(),
                new EmptyInventoryRepository(),
                new EmptySaleReturnRepository(),
                new NoOpUnitOfWork(),
                new EmptyUserRepository()),
            pdfService: pdfService);

        await viewModel.LoadFirstPageAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedInvoice = viewModel.FilteredInvoices.Single();

        (string FileName, byte[] Data)? export = null;
        viewModel.FileExportRequested += (_, args) => export = args;

        await viewModel.ExportPdfAsync(TestContext.Current.CancellationToken);

        Assert.Equal(saleId, pdfService.LastInvoiceId);
        Assert.NotNull(export);
        Assert.Equal("Factura_FAC-001.pdf", export.Value.FileName);
        Assert.Equal([1, 2, 3, 4], export.Value.Data);
        Assert.Contains("listo", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CashierFilter_RestrictsRowsToSelectedWorkerAndListsAvailableCashiers()
    {
        var viewModel = new InvoicesViewModel(
            new QuerySalesHistoryUseCase(new StaticSalesHistoryQueryService(
                new SaleHistoryResult(Guid.NewGuid(), "FAC-A", null, null, SaleStatus.Completed, DateTimeOffset.Now, 1_000m, "ana"),
                new SaleHistoryResult(Guid.NewGuid(), "FAC-B", null, null, SaleStatus.Completed, DateTimeOffset.Now, 2_000m, "beto"))),
            new ReprintReceiptUseCase(new EmptySaleRepository(), new NoOpPrintService()),
            new ProcessReturnUseCase(
                new EmptySaleRepository(),
                new EmptyProductRepository(),
                new EmptyInventoryRepository(),
                new EmptySaleReturnRepository(),
                new NoOpUnitOfWork(),
                new EmptyUserRepository()));

        await viewModel.LoadFirstPageAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, viewModel.FilteredInvoices.Count);
        Assert.Contains("ana", viewModel.AvailableCashiers);
        Assert.Contains("beto", viewModel.AvailableCashiers);

        viewModel.CashierFilter = "ana";

        InvoiceSummaryRow only = Assert.Single(viewModel.FilteredInvoices);
        Assert.Equal("FAC-A", only.InvoiceNumber);
        Assert.Equal("ana", only.CashierName);
    }

    [Fact]
    public async Task CancelSaleAsync_WhenAdministratorCancelsSelectedInvoice_UpdatesMessageAndRestoresStock()
    {
        var product = new Product(
            "Rice",
            Guid.NewGuid(),
            new POS.Domain.ValueObjects.Money(1_000m),
            new POS.Domain.ValueObjects.Money(1_500m),
            new POS.Domain.ValueObjects.StockQuantity(10m),
            new POS.Domain.ValueObjects.StockQuantity(2m));
        var sale = CreateCompletedSale(product);
        var adminUser = new User("admin", "hash", UserRole.Administrator);
        var salesRepository = new SingleSaleRepository(sale);
        var cancelSale = new CancelSaleUseCase(
            salesRepository,
            new SingleProductRepository(product),
            new RecordingInventoryRepository(),
            new SingleUserRepository(adminUser),
            new NoOpUnitOfWork());

        var viewModel = new InvoicesViewModel(
            new QuerySalesHistoryUseCase(new StaticSalesHistoryQueryService(new SaleHistoryResult(
                sale.Id,
                "FAC-002",
                null,
                "Cliente Admin",
                SaleStatus.Completed,
                DateTimeOffset.Now,
                15_000m))),
            new ReprintReceiptUseCase(new EmptySaleRepository(), new NoOpPrintService()),
            new ProcessReturnUseCase(
                new EmptySaleRepository(),
                new EmptyProductRepository(),
                new EmptyInventoryRepository(),
                new EmptySaleReturnRepository(),
                new NoOpUnitOfWork(),
                new EmptyUserRepository()),
            cancelSale: cancelSale)
        {
            OperatorUserId = adminUser.Id,
            CurrentUserRole = UserRole.Administrator
        };

        await viewModel.LoadFirstPageAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedInvoice = viewModel.FilteredInvoices.Single();
        viewModel.ReturnReason = "Producto incorrecto";

        await viewModel.CancelSaleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SaleStatus.Cancelled, sale.Status);
        Assert.Equal(10m, product.CurrentStock);
        Assert.Contains("anulada", viewModel.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, viewModel.ReturnReason);
    }

    [Fact]
    public async Task CancelSaleAsync_WhenUserIsNotAdministrator_ShowsSpanishAuthorizationMessage()
    {
        var viewModel = new InvoicesViewModel(
            new QuerySalesHistoryUseCase(new StaticSalesHistoryQueryService(new SaleHistoryResult(
                Guid.NewGuid(),
                "FAC-003",
                null,
                "Cliente Caja",
                SaleStatus.Completed,
                DateTimeOffset.Now,
                11_000m))),
            new ReprintReceiptUseCase(new EmptySaleRepository(), new NoOpPrintService()),
            new ProcessReturnUseCase(
                new EmptySaleRepository(),
                new EmptyProductRepository(),
                new EmptyInventoryRepository(),
                new EmptySaleReturnRepository(),
                new NoOpUnitOfWork(),
                new EmptyUserRepository()))
        {
            CurrentUserRole = UserRole.Cashier
        };

        await viewModel.LoadFirstPageAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedInvoice = viewModel.FilteredInvoices.Single();
        viewModel.ReturnReason = "Otro";

        await viewModel.CancelSaleAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Solo un administrador", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static Sale CreateCompletedSale(Product product)
    {
        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new POS.Domain.ValueObjects.Quantity(2m), POS.Domain.ValueObjects.Money.Zero);
        sale.AddPayment(new Payment(PaymentMethod.Cash, new POS.Domain.ValueObjects.Money(3_000m), new POS.Domain.ValueObjects.Money(3_000m)));
        sale.Complete();
        return sale;
    }

    private sealed class StaticSalesHistoryQueryService : ISalesHistoryQueryService
    {
        private readonly IReadOnlyList<SaleHistoryResult> _results;

        public StaticSalesHistoryQueryService(params SaleHistoryResult[] results) => _results = results;

        public Task<IReadOnlyList<SaleHistoryResult>> SearchAsync(
            SalesHistoryRequest request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_results);
    }

    private sealed class FakePdfService : IPdfService
    {
        private readonly byte[] _bytes;

        public FakePdfService(byte[] bytes) => _bytes = bytes;

        public Guid? LastInvoiceId { get; private set; }

        public Task<byte[]> GenerateQuotePdfAsync(Guid quoteId, CancellationToken ct = default)
            => Task.FromResult(_bytes);

        public Task<byte[]> GenerateInvoicePdfAsync(Guid saleId, CancellationToken ct = default)
        {
            LastInvoiceId = saleId;
            return Task.FromResult(_bytes);
        }
    }

    private sealed class NoOpPrintService : IPrintService
    {
        public Task PrintReceiptAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class EmptySaleRepository : ISaleRepository
    {
        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Sale?>(null);

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class SingleSaleRepository : ISaleRepository
    {
        private readonly Sale _sale;

        public SingleSaleRepository(Sale sale) => _sale = sale;

        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == _sale.Id ? _sale : null);

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class EmptyProductRepository : IProductRepository
    {
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SingleProductRepository : IProductRepository
    {
        private readonly Product _product;

        public SingleProductRepository(Product product) => _product = product;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == _product.Id ? _product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class EmptyInventoryRepository : IInventoryRepository
    {
        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryStockLevel>>([]);

        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryMovement>>([]);

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>([]);

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingInventoryRepository : IInventoryRepository
    {
        public List<InventoryMovement> Movements { get; } = [];

        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryStockLevel>>([]);

        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryMovement>>([]);

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>([]);

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            Movements.Add(movement);
            return Task.CompletedTask;
        }
    }

    private sealed class EmptySaleReturnRepository : ISaleReturnRepository
    {
        public Task AddAsync(SaleReturn saleReturn, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<SaleReturn?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.FromResult<SaleReturn?>(null);
    }

    private sealed class EmptyUserRepository : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);

        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<User>>([]);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SingleUserRepository : IUserRepository
    {
        private readonly User _user;

        public SingleUserRepository(User user) => _user = user;

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == _user.Id ? _user : null);

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => Task.FromResult(_user.Username == username ? _user : null);

        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<User>>([_user]);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
