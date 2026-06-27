using POS.Application.DTOs.Invoices;
using POS.Application.Interfaces;
using POS.Application.UseCases.Invoices;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests.Invoices;

public sealed class InvoiceUseCaseTests
{
    [Fact]
    public async Task QuerySalesHistoryAsync_DelegatesSearchRequest()
    {
        var expected = new SaleHistoryResult(Guid.NewGuid(), "FAC-0042", null, "Juan Perez", SaleStatus.Completed, DateTimeOffset.UtcNow, 10m);
        var query = new StubSalesHistoryQueryService([expected]);
        var useCase = new QuerySalesHistoryUseCase(query);

        IReadOnlyList<SaleHistoryResult> results = await useCase.ExecuteAsync(
            new SalesHistoryRequest(DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), "Perez"),
            TestContext.Current.CancellationToken);

        Assert.Single(results);
        Assert.Equal("Perez", query.LastRequest?.SearchText);
    }

    [Fact]
    public async Task ReprintReceiptAsync_WithCompletedSale_PrintsReceipt()
    {
        var sale = CreateCompletedSale(CreateProduct());
        var printer = new StubPrintService();
        var useCase = new ReprintReceiptUseCase(new InMemorySaleRepository(sale), printer);

        await useCase.ExecuteAsync(sale.Id, TestContext.Current.CancellationToken);

        Assert.Equal(sale.Id, printer.PrintedSaleId);
    }

    [Fact]
    public async Task ReprintReceiptAsync_WithReturnedSale_RejectsPrint()
    {
        var sale = CreateCompletedSale(CreateProduct());
        sale.MarkReturned();
        var useCase = new ReprintReceiptUseCase(new InMemorySaleRepository(sale), new StubPrintService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(sale.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessReturnAsync_RestoresStockRecordsMovementAndReturn()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        var inventory = new InMemoryInventoryRepository();
        var returns = new InMemorySaleReturnRepository();
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            inventory,
            returns,
            unitOfWork);

        Guid returnId = await useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, Guid.NewGuid(), "Customer refund"),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, returnId);
        Assert.Equal(SaleStatus.Returned, sale.Status);
        Assert.Equal(10m, product.CurrentStock);
        InventoryMovement movement = Assert.Single(inventory.AddedMovements);
        Assert.Equal(InventoryMovementType.Return, movement.Type);
        Assert.Equal(sale.Id, movement.ReferenceId);
        Assert.NotNull(await returns.GetBySaleIdAsync(sale.Id, TestContext.Current.CancellationToken));
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ProcessReturnAsync_WhenSaleAlreadyReturned_RejectsReturn()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        sale.MarkReturned();
        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            new InMemoryInventoryRepository(),
            new InMemorySaleReturnRepository(),
            new CountingUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, Guid.NewGuid(), "Duplicate"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessReturnAsync_PartialReturn_RestoresOnlyReturnedQty()
    {
        var product = CreateProduct();  // stock 10, qty added: 2
        var sale = CreateCompletedSale(product);  // stock now 8
        var saleItemId = sale.Items.Single().Id;
        var inventory = new InMemoryInventoryRepository();
        var returns = new InMemorySaleReturnRepository();
        var uow = new CountingUnitOfWork();
        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            inventory,
            returns,
            uow);

        // Return only 1 of the 2 items sold
        Guid returnId = await useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, Guid.NewGuid(), "Partial",
                [new ReturnLineRequest(saleItemId, 1, 1_500m)]),
            TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, returnId);
        // Stock should increase by 1 (only the returned qty)
        Assert.Equal(9m, product.CurrentStock);
        // Only one inventory movement
        Assert.Single(inventory.AddedMovements);
        Assert.Equal(1m, inventory.AddedMovements[0].Quantity);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task ProcessReturnAsync_PartialReturn_WhenQtyExceedsOriginal_Throws()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        var saleItemId = sale.Items.Single().Id;
        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            new InMemoryInventoryRepository(),
            new InMemorySaleReturnRepository(),
            new CountingUnitOfWork());

        // Original qty was 2, trying to return 3
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, Guid.NewGuid(), "Over-qty",
                [new ReturnLineRequest(saleItemId, 3, 4_500m)]),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessReturnAsync_SecondReturn_RejectedByOneReturnPerSaleGuard()
    {
        var product = CreateProduct();
        var sale = CreateCompletedSale(product);
        var returns = new InMemorySaleReturnRepository();
        var useCase = new ProcessReturnUseCase(
            new InMemorySaleRepository(sale),
            new InMemoryProductRepository(product),
            new InMemoryInventoryRepository(),
            returns,
            new CountingUnitOfWork());

        // First return succeeds
        await useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, Guid.NewGuid(), "First return"),
            TestContext.Current.CancellationToken);

        // Second return on same sale is rejected
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new ProcessReturnRequest(sale.Id, Guid.NewGuid(), "Second return"),
            TestContext.Current.CancellationToken));
    }

    private static Product CreateProduct() => new(
        "Rice",
        Guid.NewGuid(),
        new Money(1_000m),
        new Money(1_500m),
        new StockQuantity(10m),
        new StockQuantity(2m));

    private static Sale CreateCompletedSale(Product product)
    {
        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(2m), Money.Zero);
        sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(3_000m), new Money(3_000m)));
        sale.Complete();
        return sale;
    }

    private sealed class StubSalesHistoryQueryService : ISalesHistoryQueryService
    {
        private readonly IReadOnlyList<SaleHistoryResult> _results;

        public StubSalesHistoryQueryService(IReadOnlyList<SaleHistoryResult> results)
        {
            _results = results;
        }

        public SalesHistoryRequest? LastRequest { get; private set; }

        public Task<IReadOnlyList<SaleHistoryResult>> SearchAsync(SalesHistoryRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_results);
        }
    }

    private sealed class StubPrintService : IPrintService
    {
        public Guid? PrintedSaleId { get; private set; }

        public Task PrintReceiptAsync(Guid saleId, CancellationToken cancellationToken = default)
        {
            PrintedSaleId = saleId;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySaleRepository : ISaleRepository
    {
        private readonly Sale _sale;

        public InMemorySaleRepository(Sale sale)
        {
            _sale = sale;
        }

        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == _sale.Id ? _sale : null);

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly Product _product;

        public InMemoryProductRepository(Product product)
        {
            _product = product;
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == _product.Id ? _product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class InMemoryInventoryRepository : IInventoryRepository
    {
        public List<InventoryMovement> AddedMovements { get; } = [];

        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryStockLevel>>([]);

        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryMovement>>([]);

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>([]);

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            AddedMovements.Add(movement);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemorySaleReturnRepository : ISaleReturnRepository
    {
        private readonly List<SaleReturn> _returns = [];

        public Task AddAsync(SaleReturn saleReturn, CancellationToken cancellationToken = default)
        {
            _returns.Add(saleReturn);
            return Task.CompletedTask;
        }

        public Task<SaleReturn?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default)
            => Task.FromResult(_returns.SingleOrDefault(saleReturn => saleReturn.OriginalSaleId == saleId));
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
