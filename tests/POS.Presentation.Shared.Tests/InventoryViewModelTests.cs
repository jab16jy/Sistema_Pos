using POS.Application.DTOs.Inventory;
using POS.Application.UseCases.Inventory;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class InventoryViewModelTests
{
    [Fact]
    public async Task LoadStockAsync_EqualityIsLowStock_AndZeroRemainsOutOfStock()
    {
        var repository = new StaticInventoryRepository([
            new InventoryStockLevel(Guid.NewGuid(), "At minimum", "LOW", 5m, 5m, true),
            new InventoryStockLevel(Guid.NewGuid(), "Above minimum", "OK", 6m, 5m, false),
            new InventoryStockLevel(Guid.NewGuid(), "Empty", "OUT", 0m, 5m, false),
            new InventoryStockLevel(Guid.NewGuid(), "No minimum", "NONE", 4m, 0m, false)
        ]);
        var viewModel = new InventoryViewModel(
            new QueryStockUseCase(repository),
            new QueryLowStockUseCase(repository),
            new QueryMovementHistoryUseCase(repository),
            new AdjustStockUseCase(new StubProductRepository(), repository, new NoOpUnitOfWork()));

        await viewModel.LoadStockAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, viewModel.LowStockCount);
        Assert.Equal(1, viewModel.OutOfStockCount);

        viewModel.StatusFilter = "low";
        Assert.Equal("At minimum", Assert.Single(viewModel.FilteredStockLevels).ProductName);

        viewModel.StatusFilter = "ok";
        Assert.Equal(2, viewModel.FilteredStockLevels.Count);
        Assert.Contains(viewModel.FilteredStockLevels, stock => stock.ProductName == "Above minimum");
        Assert.Contains(viewModel.FilteredStockLevels, stock => stock.ProductName == "No minimum");

        viewModel.StatusFilter = "out";
        Assert.Equal("Empty", Assert.Single(viewModel.FilteredStockLevels).ProductName);
    }

    [Fact]
    public async Task LoadStockAsync_WhenQueryFails_SetsRecoverableMessage()
    {
        var viewModel = new InventoryViewModel(
            new QueryStockUseCase(new ThrowingInventoryRepository()),
            new QueryLowStockUseCase(new ThrowingInventoryRepository()),
            new QueryMovementHistoryUseCase(new ThrowingInventoryRepository()),
            new AdjustStockUseCase(new StubProductRepository(), new ThrowingInventoryRepository(), new NoOpUnitOfWork()));

        await viewModel.LoadStockAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar inventario", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticInventoryRepository : IInventoryRepository
    {
        private readonly IReadOnlyList<InventoryStockLevel> _stockLevels;

        public StaticInventoryRepository(IReadOnlyList<InventoryStockLevel> stockLevels)
        {
            _stockLevels = stockLevels;
        }

        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_stockLevels);

        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryMovement>>([]);

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>([]);

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class ThrowingInventoryRepository : IInventoryRepository
    {
        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falló stock");

        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falló movimientos");

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falló low stock");

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class StubProductRepository : IProductRepository
    {
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
