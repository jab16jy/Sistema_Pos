using System.Collections.Specialized;
using POS.Application.DTOs.Inventory;
using POS.Application.UseCases.Inventory;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class InventoryViewModelTests
{
    [Fact]
    public async Task AdjustStockAsync_PopulatesMovementHistory_BeforeClearingSelection()
    {
        var product = new Product(
            "Widget",
            Guid.NewGuid(),
            new Money(1m),
            new Money(2m),
            new StockQuantity(10m),
            new StockQuantity(1m));
        var repository = new RecordingInventoryRepository(product);
        var viewModel = new InventoryViewModel(
            new QueryStockUseCase(repository),
            new QueryLowStockUseCase(repository),
            new QueryMovementHistoryUseCase(repository),
            new AdjustStockUseCase(new StubProductRepository(product), repository, new NoOpUnitOfWork()))
        {
            SelectedProduct = new StockLevelResult(product.Id, product.Name, product.Barcode, product.CurrentStock, product.MinimumStock, false, 0m),
            AdjustmentReason = "Recuento físico",
            OperatorUserId = Guid.NewGuid()
        };

        // WPF's ListBox (SelectedItem TwoWay-bound to SelectedProduct) deselects and
        // pushes SelectedProduct = null the instant StockLevels.Clear() fires during
        // LoadStockAsync. Simulate that side effect so the test fails without the
        // reselection-by-id fix in LoadStockAsync.
        viewModel.StockLevels.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                viewModel.SelectedProduct = null;
            }
        };

        await viewModel.AdjustStockAsync(TestContext.Current.CancellationToken);

        Assert.Single(viewModel.MovementHistory);
    }

    [Fact]
    public void SelectingProduct_LoadsMovementHistory_WithoutRequiringAnyAdjustment()
    {
        var product = new Product(
            "Gadget",
            Guid.NewGuid(),
            new Money(1m),
            new Money(2m),
            new StockQuantity(5m),
            new StockQuantity(1m));
        var repository = new RecordingInventoryRepository(product);
        repository.Seed(new InventoryMovement(product.Id, InventoryMovementType.InitialBalance, new Quantity(5m), "Saldo inicial", Guid.NewGuid()));
        var viewModel = new InventoryViewModel(
            new QueryStockUseCase(repository),
            new QueryLowStockUseCase(repository),
            new QueryMovementHistoryUseCase(repository),
            new AdjustStockUseCase(new StubProductRepository(product), repository, new NoOpUnitOfWork()));

        viewModel.SelectedProduct = new StockLevelResult(product.Id, product.Name, product.Barcode, product.CurrentStock, product.MinimumStock, false, 0m);

        Assert.Single(viewModel.MovementHistory);
    }

    [Fact]
    public void LoadMovementHistoryAsync_ConvertsCreatedAtToLocalTime()
    {
        var product = new Product(
            "Widget",
            Guid.NewGuid(),
            new Money(1m),
            new Money(2m),
            new StockQuantity(10m),
            new StockQuantity(1m));
        var repository = new RecordingInventoryRepository(product);
        var movement = new InventoryMovement(product.Id, InventoryMovementType.InitialBalance, new Quantity(10m), "Saldo inicial", Guid.NewGuid());
        repository.Seed(movement);
        var viewModel = new InventoryViewModel(
            new QueryStockUseCase(repository),
            new QueryLowStockUseCase(repository),
            new QueryMovementHistoryUseCase(repository),
            new AdjustStockUseCase(new StubProductRepository(product), repository, new NoOpUnitOfWork()));

        viewModel.SelectedProduct = new StockLevelResult(product.Id, product.Name, product.Barcode, product.CurrentStock, product.MinimumStock, false, 0m);

        var displayed = Assert.Single(viewModel.MovementHistory);
        Assert.Equal(movement.CreatedAt.ToLocalTime().Offset, displayed.CreatedAt.Offset);
    }

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
        private readonly Product? _product;

        public StubProductRepository(Product? product = null)
        {
            _product = product;
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_product is not null && _product.Id == id ? _product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingInventoryRepository : IInventoryRepository
    {
        private readonly Product _product;
        private readonly List<InventoryMovement> _movements = [];

        public RecordingInventoryRepository(Product product)
        {
            _product = product;
        }

        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryStockLevel>>([
                new InventoryStockLevel(_product.Id, _product.Name, _product.Barcode, _product.CurrentStock, _product.MinimumStock, false)
            ]);

        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<InventoryMovement>>(_movements.Where(m => m.ProductId == productId).ToList());

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>([]);

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            _movements.Add(movement);
            return Task.CompletedTask;
        }

        public void Seed(InventoryMovement movement) => _movements.Add(movement);
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
