using POS.Application.DTOs.Products;
using POS.Application.Interfaces;
using POS.Application.UseCases.Categories;
using POS.Application.UseCases.Inventory;
using POS.Application.UseCases.Products;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

/// <summary>
/// Task 5: editing an existing product's stock must persist the new total AND leave an
/// audited InventoryMovement (UpdateProductUseCase deliberately does not touch stock, so the
/// ViewModel routes the delta through AdjustStockUseCase).
/// </summary>
public sealed class ProductsViewModelStockEditTests
{
    [Fact]
    public async Task EditingStock_PersistsNewTotal_AndRecordsAuditedAdjustment()
    {
        Category category = new("Sin categoría");
        var product = new Product(
            "Aceite",
            category.Id,
            new Money(15_000m),
            new Money(25_000m),
            new StockQuantity(10m),
            new StockQuantity(1m));

        var productRepo = new StoringProductRepository(product);
        var categoryRepo = new SingleCategoryRepository(category);
        var inventoryRepo = new RecordingInventoryRepository();
        var unitOfWork = new NoOpUnitOfWork();
        var queryService = new StubQueryService([
            new ProductSearchResult(product.Id, "Aceite", category.Id, "Sin categoría", 15_000m, 25_000m, 10m, 1m, 19m, null, null, true)
        ]);

        var viewModel = new ProductsViewModel(
            new CreateProductUseCase(productRepo, categoryRepo, unitOfWork),
            new UpdateProductUseCase(productRepo, categoryRepo, unitOfWork),
            new DeleteProductUseCase(productRepo, unitOfWork),
            new SearchProductsUseCase(queryService),
            new SearchCategoriesUseCase(categoryRepo),
            null,
            new AdjustStockUseCase(productRepo, inventoryRepo, unitOfWork))
        {
            OperatorUserId = Guid.NewGuid()
        };

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedProduct = viewModel.Products.Single();
        Assert.Equal(10m, viewModel.CurrentStock);

        viewModel.CurrentStock = 25m;
        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        // Stock persisted on the entity.
        Assert.Equal(25m, product.CurrentStock);

        // Exactly one audited adjustment for the +15 delta.
        InventoryMovement movement = Assert.Single(inventoryRepo.Movements);
        Assert.Equal(InventoryMovementType.Adjustment, movement.Type);
        Assert.Equal(15m, movement.Quantity);
        Assert.Equal(product.Id, movement.ProductId);
    }

    [Fact]
    public async Task SavingWithoutStockChange_RecordsNoAdjustment()
    {
        Category category = new("Sin categoría");
        var product = new Product(
            "Aceite",
            category.Id,
            new Money(15_000m),
            new Money(25_000m),
            new StockQuantity(10m),
            new StockQuantity(1m));

        var productRepo = new StoringProductRepository(product);
        var categoryRepo = new SingleCategoryRepository(category);
        var inventoryRepo = new RecordingInventoryRepository();
        var unitOfWork = new NoOpUnitOfWork();
        var queryService = new StubQueryService([
            new ProductSearchResult(product.Id, "Aceite", category.Id, "Sin categoría", 15_000m, 25_000m, 10m, 1m, 19m, null, null, true)
        ]);

        var viewModel = new ProductsViewModel(
            new CreateProductUseCase(productRepo, categoryRepo, unitOfWork),
            new UpdateProductUseCase(productRepo, categoryRepo, unitOfWork),
            new DeleteProductUseCase(productRepo, unitOfWork),
            new SearchProductsUseCase(queryService),
            new SearchCategoriesUseCase(categoryRepo),
            null,
            new AdjustStockUseCase(productRepo, inventoryRepo, unitOfWork))
        {
            OperatorUserId = Guid.NewGuid()
        };

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedProduct = viewModel.Products.Single();

        // Change only the sale price, not the stock.
        viewModel.SalePrice = 30_000m;
        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        Assert.Empty(inventoryRepo.Movements);
        Assert.Equal(10m, product.CurrentStock);
    }

    private sealed class StoringProductRepository : IProductRepository
    {
        private readonly Product _product;

        public StoringProductRepository(Product product) => _product = product;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(_product.Id == id ? _product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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
            => Task.FromResult<IReadOnlyList<InventoryMovement>>([]);

        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>([]);

        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
        {
            Movements.Add(movement);
            return Task.CompletedTask;
        }
    }

    private sealed class StubQueryService : IProductCatalogQueryService
    {
        private readonly IReadOnlyList<ProductSearchResult> _results;

        public StubQueryService(IReadOnlyList<ProductSearchResult> results) => _results = results;

        public Task<IReadOnlyList<ProductSearchResult>> SearchAsync(SearchProductsRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_results);
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
