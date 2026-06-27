using POS.Application.DTOs.Inventory;
using POS.Application.UseCases.Inventory;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests.Inventory;

public sealed class InventoryUseCaseTests
{
    [Fact]
    public async Task QueryStockAsync_ReturnsDeficitForLowStockProducts()
    {
        var inventory = new InMemoryInventoryRepository
        {
            StockLevels =
            [
                new InventoryStockLevel(Guid.NewGuid(), "Rice", "A1", 5m, 10m, true),
                new InventoryStockLevel(Guid.NewGuid(), "Beans", null, 20m, 5m, false)
            ]
        };
        var useCase = new QueryStockUseCase(inventory);

        IReadOnlyList<StockLevelResult> results = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.Equal(5m, results[0].Deficit);
        Assert.Equal(0m, results[1].Deficit);
    }

    [Fact]
    public async Task QueryLowStockAsync_ExcludesProductsWithoutMinimumStock()
    {
        var low = CreateProduct("Rice", 5m, 10m);
        var noMinimum = CreateProduct("Beans", 0m, 0m);
        var useCase = new QueryLowStockUseCase(new InMemoryInventoryRepository { LowStockProducts = [low, noMinimum] });

        IReadOnlyList<LowStockProductResult> results = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        LowStockProductResult result = Assert.Single(results);
        Assert.Equal(low.Id, result.ProductId);
        Assert.Equal(5m, result.Deficit);
    }

    [Fact]
    public async Task QueryMovementHistoryAsync_ReturnsReverseChronologicalResults()
    {
        var productId = Guid.NewGuid();
        var older = new InventoryMovement(productId, InventoryMovementType.Entry, new Quantity(1m), "Purchase", Guid.NewGuid());
        await Task.Delay(2, TestContext.Current.CancellationToken);
        var newer = new InventoryMovement(productId, InventoryMovementType.Adjustment, new Quantity(2m), "Count", Guid.NewGuid());
        var useCase = new QueryMovementHistoryUseCase(new InMemoryInventoryRepository { Movements = [older, newer] });

        IReadOnlyList<InventoryMovementResult> results = await useCase.ExecuteAsync(productId, TestContext.Current.CancellationToken);

        Assert.Equal(newer.Id, results[0].Id);
        Assert.Equal(older.Id, results[1].Id);
    }

    [Fact]
    public async Task AdjustStockAsync_IncreasesStockAndRecordsMovement()
    {
        var product = CreateProduct("Rice", 20m, 5m);
        var inventory = new InMemoryInventoryRepository();
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new AdjustStockUseCase(new InMemoryProductRepository(product), inventory, unitOfWork);

        StockLevelResult result = await useCase.ExecuteAsync(
            new AdjustStockRequest(product.Id, 5m, "Count correction", Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Equal(25m, product.CurrentStock);
        Assert.Equal(25m, result.CurrentStock);
        InventoryMovement movement = Assert.Single(inventory.AddedMovements);
        Assert.Equal(InventoryMovementType.Adjustment, movement.Type);
        Assert.Equal(5m, movement.Quantity);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AdjustStockAsync_DecreasesStockAndRecordsMovement()
    {
        var product = CreateProduct("Rice", 20m, 5m);
        var inventory = new InMemoryInventoryRepository();
        var useCase = new AdjustStockUseCase(new InMemoryProductRepository(product), inventory, new CountingUnitOfWork());

        await useCase.ExecuteAsync(
            new AdjustStockRequest(product.Id, -3m, "Damaged goods", Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.Equal(17m, product.CurrentStock);
        Assert.Equal(3m, Assert.Single(inventory.AddedMovements).Quantity);
    }

    private static Product CreateProduct(string name, decimal currentStock, decimal minimumStock) => new(
        name,
        Guid.NewGuid(),
        new Money(1_000m),
        new Money(1_500m),
        new StockQuantity(currentStock),
        new StockQuantity(minimumStock));

    private sealed class InMemoryInventoryRepository : IInventoryRepository
    {
        public IReadOnlyList<InventoryStockLevel> StockLevels { get; init; } = [];
        public IReadOnlyList<Product> LowStockProducts { get; init; } = [];
        public IReadOnlyList<InventoryMovement> Movements { get; init; } = [];
        public List<InventoryMovement> AddedMovements { get; } = [];

        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(StockLevels);

        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryMovement>>(Movements.Where(movement => movement.ProductId == productId).ToList());

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(LowStockProducts);

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            AddedMovements.Add(movement);
            return Task.CompletedTask;
        }
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
