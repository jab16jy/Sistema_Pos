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
