using POS.Application.DTOs.Products;
using POS.Application.DTOs.BarcodeLabels;
using POS.Application.Interfaces;
using POS.Application.UseCases.BarcodeLabels;
using POS.Application.UseCases.Categories;
using POS.Application.UseCases.Products;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class ProductsViewModelTests
{
    [Fact]
    public async Task LoadAsync_LoadsProductsCategoriesAndDefaultCategory()
    {
        Category defaultCategory = new("Sin categoría");
        Category secondaryCategory = new("Verduras");
        var products = new StubProductCatalogQueryService([
            new ProductSearchResult(Guid.NewGuid(), "Aceite", defaultCategory.Id, "Sin categoría", null, 25_000m, 3m, 1m, 19m, null, "ACE-1", true)
        ]);
        var viewModel = CreateViewModel(products, [
            defaultCategory,
            secondaryCategory
        ]);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Single(viewModel.Products);
        Assert.Equal("Sin categoría", viewModel.Products[0].CategoryName);
        Assert.Equal(defaultCategory.Id, viewModel.CategoryId);
        Assert.Equal(2, viewModel.Categories.Count);
    }

    [Fact]
    public async Task SelectingImportedProduct_PreservesNullCostPrice()
    {
        Category defaultCategory = new("Sin categoría");
        ProductSearchResult product = new(Guid.NewGuid(), "Aceite", defaultCategory.Id, "Sin categoría", null, 25_000m, 3m, 1m, 19m, null, "ACE-1", true);
        var viewModel = CreateViewModel(new StubProductCatalogQueryService([product]), [defaultCategory]);

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedProduct = viewModel.Products.Single();

        Assert.Null(viewModel.CostPrice);
    }

    [Fact]
    public async Task LoadAsync_WhenSearchFails_SetsRecoverableMessage()
    {
        var viewModel = new ProductsViewModel(
            new CreateProductUseCase(new StubProductRepository(), new StubCategoryRepository([]), new NoOpUnitOfWork()),
            new UpdateProductUseCase(new StubProductRepository(), new StubCategoryRepository([]), new NoOpUnitOfWork()),
            new DeleteProductUseCase(new StubProductRepository(), new NoOpUnitOfWork()),
            new SearchProductsUseCase(new ThrowingProductCatalogQueryService()));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar productos", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PrintLabelAsync_UsesTheSelectedProductSavedBarcodeNameAndPrice()
    {
        Category category = new("Sin categoría");
        var product = new ProductSearchResult(
            Guid.NewGuid(),
            "Crema facial",
            category.Id,
            category.Name,
            10_000m,
            25_000m,
            3m,
            1m,
            19m,
            null,
            "4006381333931",
            true);
        var printer = new RecordingLabelPrintService();
        var productRepository = new StubProductRepository();
        var viewModel = new ProductsViewModel(
            new CreateProductUseCase(productRepository, new StubCategoryRepository([category]), new NoOpUnitOfWork()),
            new UpdateProductUseCase(productRepository, new StubCategoryRepository([category]), new NoOpUnitOfWork()),
            new DeleteProductUseCase(productRepository, new NoOpUnitOfWork()),
            new SearchProductsUseCase(new StubProductCatalogQueryService([product])),
            new SearchCategoriesUseCase(new StubCategoryRepository([category])),
            printLabels: new PrintLabelsUseCase(printer));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedProduct = Assert.Single(viewModel.Products);
        await viewModel.PrintLabelAsync(TestContext.Current.CancellationToken);

        LabelRequest label = Assert.Single(printer.Labels);
        Assert.Equal("Crema facial", label.ProductName);
        Assert.Equal(25_000m, label.Price);
        Assert.Equal("4006381333931", label.BarcodeValue);
        Assert.Equal(BarcodeSymbology.Ean13, label.Symbology);
    }

    private static ProductsViewModel CreateViewModel(
        IProductCatalogQueryService productCatalogQueryService,
        IReadOnlyList<Category> categories)
    {
        var productRepository = new StubProductRepository();
        var categoryRepository = new StubCategoryRepository(categories);

        return new ProductsViewModel(
            new CreateProductUseCase(productRepository, categoryRepository, new NoOpUnitOfWork()),
            new UpdateProductUseCase(productRepository, categoryRepository, new NoOpUnitOfWork()),
            new DeleteProductUseCase(productRepository, new NoOpUnitOfWork()),
            new SearchProductsUseCase(productCatalogQueryService),
            new SearchCategoriesUseCase(categoryRepository));
    }

    private sealed class StubProductCatalogQueryService : IProductCatalogQueryService
    {
        private readonly IReadOnlyList<ProductSearchResult> _results;

        public StubProductCatalogQueryService(IReadOnlyList<ProductSearchResult> results)
        {
            _results = results;
        }

        public Task<IReadOnlyList<ProductSearchResult>> SearchAsync(SearchProductsRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_results);
    }

    private sealed class ThrowingProductCatalogQueryService : IProductCatalogQueryService
    {
        public Task<IReadOnlyList<ProductSearchResult>> SearchAsync(SearchProductsRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falló búsqueda");
    }

    private sealed class StubCategoryRepository : ICategoryRepository
    {
        private readonly IReadOnlyList<Category> _categories;

        public StubCategoryRepository(IReadOnlyList<Category> categories)
        {
            _categories = categories;
        }

        public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_categories);

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_categories.SingleOrDefault(category => category.Id == id));

        public Task AddAsync(Category category, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Category category, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> HasProductsAsync(Guid categoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
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

    private sealed class RecordingLabelPrintService : ILabelPrintService
    {
        public List<LabelRequest> Labels { get; } = [];

        public Task PrintAsync(IEnumerable<LabelRequest> labels, int copies = 1, CancellationToken cancellationToken = default)
        {
            Labels.AddRange(labels);
            return Task.CompletedTask;
        }
    }
}
