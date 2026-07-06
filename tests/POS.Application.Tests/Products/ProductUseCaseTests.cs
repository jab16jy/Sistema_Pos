using POS.Application.DTOs.Products;
using POS.Application.Interfaces;
using POS.Application.UseCases.Products;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests.Products;

public sealed class ProductUseCaseTests
{
    [Fact]
    public async Task CreateProductAsync_WithDuplicateBarcode_RejectsCreation()
    {
        var existing = CreateProduct("Rice", "A100");
        var products = new InMemoryProductRepository(existing);
        var category = new Category("Abarrotes");
        var useCase = new CreateProductUseCase(products, new InMemoryCategoryRepository(category), new CountingUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new CreateProductRequest(
            "Beans",
            category.Id,
            1_000m,
            1_500m,
            0m,
            2m,
            null,
            "A100"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateProductAsync_WithNullBarcode_CreatesProduct()
    {
        var products = new InMemoryProductRepository();
        var unitOfWork = new CountingUnitOfWork();
        var category = new Category("Abarrotes");
        var useCase = new CreateProductUseCase(products, new InMemoryCategoryRepository(category), unitOfWork);

        Guid productId = await useCase.ExecuteAsync(new CreateProductRequest(
            "Beans",
            category.Id,
            1_000m,
            1_500m,
            0m,
            2m,
            null,
            null), TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, productId);
        Assert.Single(products.Products);
        Assert.Null(products.Products[0].Barcode);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateProductAsync_WithValidData_MutatesProductAndSaves()
    {
        var product = CreateProduct("Rice", "A100");
        var products = new InMemoryProductRepository(product);
        var unitOfWork = new CountingUnitOfWork();
        var category = new Category("Premium");
        var useCase = new UpdateProductUseCase(products, new InMemoryCategoryRepository(category), unitOfWork);

        ProductResult result = await useCase.ExecuteAsync(new UpdateProductRequest(
            product.Id,
            "Premium Rice",
            category.Id,
            1_100m,
            1_700m,
            4m,
            "White rice",
            "B200"), TestContext.Current.CancellationToken);

        Assert.Equal("Premium Rice", product.Name);
        Assert.Equal("B200", product.Barcode);
        Assert.Equal(1_700m, result.SalePrice);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateProductAsync_WithDuplicateBarcode_RejectsUpdate()
    {
        var product = CreateProduct("Rice", "A100");
        var other = CreateProduct("Beans", "B200");
        var category = new Category("Abarrotes");
        product.UpdateDetails(product.Name, category.Id, product.CostPrice, product.SalePrice, new StockQuantity(product.MinimumStock), product.Description, product.Barcode is null ? null : new Barcode(product.Barcode), product.TaxRate);
        var useCase = new UpdateProductUseCase(new InMemoryProductRepository(product, other), new InMemoryCategoryRepository(category), new CountingUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new UpdateProductRequest(
            product.Id,
            "Rice",
            product.CategoryId,
            1_000m,
            1_500m,
            2m,
            null,
            "B200"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteProductAsync_DeactivatesProductAndSaves()
    {
        var product = CreateProduct("Rice", "A100");
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new DeleteProductUseCase(new InMemoryProductRepository(product), unitOfWork);

        await useCase.ExecuteAsync(product.Id, TestContext.Current.CancellationToken);

        Assert.False(product.IsActive);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task SearchProductsAsync_DelegatesToCatalogQuery()
    {
        Guid categoryId = Guid.NewGuid();
        var expected = new ProductSearchResult(Guid.NewGuid(), "Aspirina", categoryId, "Sin categoría", null, 10m, 5m, 2m, 19m, null, "ABC", true);
        var query = new StubProductCatalogQueryService([expected]);
        var useCase = new SearchProductsUseCase(query);

        IReadOnlyList<ProductSearchResult> results = await useCase.ExecuteAsync(
            new SearchProductsRequest("Aspir", CategoryId: categoryId),
            TestContext.Current.CancellationToken);

        ProductSearchResult result = Assert.Single(results);
        Assert.Equal(expected.Id, result.Id);
        Assert.Equal("Aspir", query.LastRequest?.SearchText);
        Assert.Equal(categoryId, query.LastRequest?.CategoryId);
    }

    [Fact]
    public async Task ImportProductsCsvAsync_WithValidRows_CreatesProductsWithTaxRate()
    {
        var categoryId = Guid.NewGuid();
        var products = new InMemoryProductRepository();
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new ImportProductsCsvUseCase(products, unitOfWork);
        var csv = string.Join('\n',
            "name,categoryId,costPrice,salePrice,currentStock,minimumStock,description,barcode,taxRate",
            $"Cafe,{categoryId},2000,11900,5,1,Bebida,CAF001,19");

        ImportProductsCsvResult result = await useCase.ExecuteAsync(new ImportProductsCsvRequest(csv), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Imported);
        Assert.Equal(0, result.Skipped);
        Assert.Empty(result.Errors);
        Assert.Single(products.Products);
        Assert.Equal(19m, products.Products[0].TaxRate);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private static Product CreateProduct(string name, string? barcode) => new(
        name,
        Guid.NewGuid(),
        new Money(1_000m),
        new Money(1_500m),
        new StockQuantity(10m),
        new StockQuantity(2m),
        null,
        barcode is null ? null : new Barcode(barcode));

    private sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly List<Product> _products;

        public InMemoryProductRepository(params Product[] products)
        {
            _products = [.. products];
        }

        public IReadOnlyList<Product> Products => _products;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_products.SingleOrDefault(product => product.Id == id));

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult(_products.SingleOrDefault(product => product.Barcode == barcode && product.IsActive));

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            _products.Add(product);
            return Task.CompletedTask;
        }
    }

    private sealed class StubProductCatalogQueryService : IProductCatalogQueryService
    {
        private readonly IReadOnlyList<ProductSearchResult> _results;

        public StubProductCatalogQueryService(IReadOnlyList<ProductSearchResult> results)
        {
            _results = results;
        }

        public SearchProductsRequest? LastRequest { get; private set; }

        public Task<IReadOnlyList<ProductSearchResult>> SearchAsync(SearchProductsRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(_results);
        }
    }

    private sealed class InMemoryCategoryRepository : ICategoryRepository
    {
        private readonly Dictionary<Guid, Category> _categories;

        public InMemoryCategoryRepository(params Category[] categories)
        {
            _categories = categories.ToDictionary(category => category.Id);
        }

        public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Category>>(_categories.Values.ToList());

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_categories.GetValueOrDefault(id));

        public Task AddAsync(Category category, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(Category category, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> HasProductsAsync(Guid categoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
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
