using POS.Application.DTOs.Products;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Products;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests.Inventory;

/// <summary>
/// The inventory invariant: every change to CurrentStock corresponds to exactly one persisted
/// InventoryMovement created in the same unit of work, so saldo actual e historial reconcile and
/// stock can never go negative.
/// </summary>
public sealed class StockTraceabilityTests
{
    private static readonly Guid Operator = Guid.NewGuid();

    [Fact]
    public async Task CreateThenSell_RecordsInitialBalanceAndExit_AndReconcilesWithHistory()
    {
        var category = new Category("Abarrotes");
        var products = new ListProductRepository();
        var inventory = new RecordingInventoryRepository();
        var unitOfWork = new CountingUnitOfWork();

        var create = new CreateProductUseCase(products, new SingleCategoryRepository(category), unitOfWork, inventory);
        Guid productId = await create.ExecuteAsync(
            new CreateProductRequest("Arroz", category.Id, 1_000m, 1_500m, 10m, 2m, null, null, OperatorUserId: Operator),
            TestContext.Current.CancellationToken);

        var sell = new CreateSaleUseCase(products, new ListSaleRepository(), unitOfWork, null, null, null, inventory);
        await sell.ExecuteAsync(
            new CreateSaleRequest(Operator, null,
                [new CreateSaleItemRequest(productId, 3m, 0m)],
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 4_500m, 4_500m, null)]),
            TestContext.Current.CancellationToken);

        Product product = (await products.GetByIdAsync(productId, TestContext.Current.CancellationToken))!;
        Assert.Equal(7m, product.CurrentStock);

        // One InitialBalance (+10) and one Exit (-3) referencing the sale.
        Assert.Equal(2, inventory.Movements.Count);
        InventoryMovement initial = inventory.Movements.Single(m => m.Type == InventoryMovementType.InitialBalance);
        Assert.Equal(10m, initial.Quantity);
        InventoryMovement exit = inventory.Movements.Single(m => m.Type == InventoryMovementType.Exit);
        Assert.Equal(3m, exit.Quantity);
        Assert.NotNull(exit.ReferenceId);

        // Current stock equals the signed sum of its movement history.
        decimal reconciled = inventory.Movements.Sum(Signed);
        Assert.Equal(product.CurrentStock, reconciled);
    }

    [Fact]
    public async Task SellBeyondStock_Throws_AndPersistsNoSaleOrMovement()
    {
        var category = new Category("Abarrotes");
        var products = new ListProductRepository();
        var inventory = new RecordingInventoryRepository();
        var unitOfWork = new CountingUnitOfWork();

        var create = new CreateProductUseCase(products, new SingleCategoryRepository(category), unitOfWork, inventory);
        Guid productId = await create.ExecuteAsync(
            new CreateProductRequest("Sal", category.Id, 500m, 800m, 5m, 1m, null, null, OperatorUserId: Operator),
            TestContext.Current.CancellationToken);

        int savesAfterCreate = unitOfWork.SaveCount;
        int movementsAfterCreate = inventory.Movements.Count;
        var sales = new ListSaleRepository();
        var sell = new CreateSaleUseCase(products, sales, unitOfWork, null, null, null, inventory);

        await Assert.ThrowsAsync<InsufficientStockException>(() => sell.ExecuteAsync(
            new CreateSaleRequest(Operator, null,
                [new CreateSaleItemRequest(productId, 10m, 0m)],
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 8_000m, 8_000m, null)]),
            TestContext.Current.CancellationToken));

        Product product = (await products.GetByIdAsync(productId, TestContext.Current.CancellationToken))!;
        Assert.Equal(5m, product.CurrentStock);              // stock untouched
        Assert.Empty(sales.Sales);                            // no sale persisted
        Assert.Equal(movementsAfterCreate, inventory.Movements.Count); // no Exit movement
        Assert.Equal(savesAfterCreate, unitOfWork.SaveCount); // never reached SaveChanges
    }

    // Exit removes stock; every other recorded type adds it (a signed Adjustment would be ambiguous
    // in this abs-quantity schema, so this reconciliation covers the unambiguous movement types).
    private static decimal Signed(InventoryMovement movement)
        => movement.Type == InventoryMovementType.Exit ? -movement.Quantity : movement.Quantity;

    private sealed class ListProductRepository : IProductRepository
    {
        private readonly List<Product> _products = [];

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_products.FirstOrDefault(p => p.Id == id));

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult(_products.FirstOrDefault(p => p.Barcode == barcode));

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            _products.Add(product);
            return Task.CompletedTask;
        }
    }

    private sealed class SingleCategoryRepository : ICategoryRepository
    {
        private readonly Category _category;

        public SingleCategoryRepository(Category category) => _category = category;

        public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Category>>([_category]);

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Category?>(_category.Id == id ? _category : null);

        public Task AddAsync(Category category, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Category category, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> HasProductsAsync(Guid categoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingInventoryRepository : IInventoryRepository
    {
        public List<InventoryMovement> Movements { get; } = [];

        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryStockLevel>>([]);

        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryMovement>>(Movements.Where(m => m.ProductId == productId).ToList());

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>([]);

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            Movements.Add(movement);
            return Task.CompletedTask;
        }
    }

    private sealed class ListSaleRepository : ISaleRepository
    {
        public List<Sale> Sales { get; } = [];

        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
        {
            Sales.Add(sale);
            return Task.CompletedTask;
        }

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Sales.FirstOrDefault(s => s.Id == id));

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
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
