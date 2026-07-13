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
/// Productos administers stock as master data only: it can seed an initial balance when creating a
/// product (recorded as an InventoryMovement), but stock is read-only when editing — every later
/// change belongs to Inventario/Compras/Ventas so saldo e historial stay reconcilable.
/// </summary>
public sealed class ProductsViewModelStockEditTests
{
    [Fact]
    public async Task EditingExistingProduct_StockIsReadOnly_AndNeverAdjusts()
    {
        Category category = new("Sin categoría");
        var product = new Product(
            "Aceite",
            category.Id,
            new Money(15_000m),
            new Money(25_000m),
            new StockQuantity(10m),
            new StockQuantity(1m));

        var productRepo = new CapturingProductRepository(product);
        var categoryRepo = new SingleCategoryRepository(category);
        var inventoryRepo = new RecordingInventoryRepository();
        var unitOfWork = new NoOpUnitOfWork();
        var queryService = new StubQueryService([
            new ProductSearchResult(product.Id, "Aceite", category.Id, "Sin categoría", 15_000m, 25_000m, 10m, 1m, 19m, null, null, true)
        ]);

        var viewModel = new ProductsViewModel(
            new CreateProductUseCase(productRepo, categoryRepo, unitOfWork, inventoryRepo),
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

        // Stock cannot be edited from the product form when a product is selected.
        Assert.False(viewModel.IsStockEditable);
        Assert.Equal("Stock actual", viewModel.StockFieldLabel);

        // Even if the bound value is forced, saving an edit must not move stock or record a movement.
        viewModel.CurrentStock = 25m;
        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        Assert.Equal(10m, product.CurrentStock);
        Assert.Empty(inventoryRepo.Movements);
    }

    [Fact]
    public async Task CreatingProductWithInitialStock_SeedsViaAuditedInitialBalanceMovement()
    {
        Category category = new("Sin categoría");
        var productRepo = new CapturingProductRepository();
        var categoryRepo = new SingleCategoryRepository(category);
        var inventoryRepo = new RecordingInventoryRepository();
        var unitOfWork = new NoOpUnitOfWork();
        var queryService = new StubQueryService([]);

        var viewModel = new ProductsViewModel(
            new CreateProductUseCase(productRepo, categoryRepo, unitOfWork, inventoryRepo),
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

        // No product selected → creating a new one. The stock field is the initial balance.
        Assert.True(viewModel.IsStockEditable);
        Assert.Equal("Existencia inicial", viewModel.StockFieldLabel);

        viewModel.Name = "Arroz";
        viewModel.CategoryId = category.Id;
        viewModel.SalePrice = 3_000m;
        viewModel.CurrentStock = 20m;
        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        // Product seeded to exactly the initial balance, reached through a single audited movement.
        Product created = Assert.Single(productRepo.Added);
        Assert.Equal(20m, created.CurrentStock);

        InventoryMovement movement = Assert.Single(inventoryRepo.Movements);
        Assert.Equal(InventoryMovementType.InitialBalance, movement.Type);
        Assert.Equal(20m, movement.Quantity);
        Assert.Equal(created.Id, movement.ProductId);
    }

    private sealed class CapturingProductRepository : IProductRepository
    {
        private readonly Product? _preset;

        public CapturingProductRepository(Product? preset = null) => _preset = preset;

        public List<Product> Added { get; } = [];

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(_preset is not null && _preset.Id == id ? _preset : Added.FirstOrDefault(p => p.Id == id));

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            Added.Add(product);
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
