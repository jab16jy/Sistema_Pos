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
    public async Task AssignInternalBarcodeAsync_PersistsAUniqueCode128Value()
    {
        var product = CreateProduct("Rice", null);
        var products = new InMemoryProductRepository(product);
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new AssignInternalBarcodeUseCase(products, unitOfWork);

        string barcode = await useCase.ExecuteAsync(product.Id, TestContext.Current.CancellationToken);

        Assert.Equal($"INT-{product.Id:N}", barcode);
        Assert.Equal(barcode, product.Barcode);
        Assert.Equal(1, unitOfWork.SaveCount);
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
    public async Task AssignProductsToCategoryAsync_UpdatesAllSelectedProductsWithOneSave()
    {
        var first = CreateProduct("Rice", "A100");
        var second = CreateProduct("Beans", "B200");
        var category = new Category("Pantry");
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new AssignProductsToCategoryUseCase(
            new InMemoryProductRepository(first, second),
            new InMemoryCategoryRepository(category),
            unitOfWork);

        int updated = await useCase.ExecuteAsync(
            new AssignProductsToCategoryRequest(category.Id, [first.Id, second.Id]),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, updated);
        Assert.Equal(category.Id, first.CategoryId);
        Assert.Equal(category.Id, second.CategoryId);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AssignProductsToCategoryAsync_WithMissingProduct_LeavesOtherProductsUnchanged()
    {
        var product = CreateProduct("Rice", "A100");
        var originalCategoryId = product.CategoryId;
        var category = new Category("Pantry");
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new AssignProductsToCategoryUseCase(
            new InMemoryProductRepository(product),
            new InMemoryCategoryRepository(category),
            unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new AssignProductsToCategoryRequest(category.Id, [product.Id, Guid.NewGuid()]),
            TestContext.Current.CancellationToken));

        Assert.Equal(originalCategoryId, product.CategoryId);
        Assert.Equal(0, unitOfWork.SaveCount);
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

    [Fact]
    public async Task ImportProductsCsvAsync_SkipsSameBatchDuplicateBarcodeUsingExactIdentity()
    {
        var categoryId = Guid.NewGuid();
        var products = new InMemoryProductRepository();
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new ImportProductsCsvUseCase(products, unitOfWork);
        var csv = string.Join('\n',
            "name,categoryId,costPrice,salePrice,currentStock,minimumStock,description,barcode,taxRate",
            $"First,{categoryId},2000,11900,5,1,Bebida,CODE-001,19",
            $"Duplicate,{categoryId},2000,11900,5,1,Bebida,CODE-001,19",
            $"Case distinct,{categoryId},2000,11900,5,1,Bebida,code-001,19");

        ImportProductsCsvResult result = await useCase.ExecuteAsync(new ImportProductsCsvRequest(csv), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Imported);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(["CODE-001", "code-001"], products.Products.Select(product => product.Barcode));
        Assert.Contains(result.Errors, error => error.Contains("Línea 3", StringComparison.Ordinal));
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ImportProductsCsvAsync_InvalidFirstRowDoesNotBlockValidRowWithSameBarcode()
    {
        var categoryId = Guid.NewGuid();
        var products = new InMemoryProductRepository();
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new ImportProductsCsvUseCase(products, unitOfWork);
        // Row 2 has a valid barcode but Row 1 (same barcode) is missing the required "name" field.
        var csv = string.Join('\n',
            "name,categoryId,costPrice,salePrice,currentStock,minimumStock,taxRate,barcode",
            $",{categoryId},2000,11900,5,1,19,ROW-BLOCK",
            $"Valid,{categoryId},2000,11900,5,1,19,ROW-BLOCK");

        ImportProductsCsvResult result = await useCase.ExecuteAsync(new ImportProductsCsvRequest(csv), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Skipped);
        Product imported = Assert.Single(products.Products);
        Assert.Equal("ROW-BLOCK", imported.Barcode);
    }

    [Fact]
    public async Task ImportProductsCsvAsync_WithExternalAccountingExportSchema_AutoCreatesCategoryAndClampsNegativeStock()
    {
        var products = new InMemoryProductRepository();
        var categories = new InMemoryCategoryRepository();
        var unitOfWork = new CountingUnitOfWork();
        var useCase = new ImportProductsCsvUseCase(products, unitOfWork, categories: categories);
        // Mirrors an Alegra/Siigo-style export: 3 preamble rows, then Tipo,Código,Nombre,... headers.
        var csv = string.Join('\n',
            "Gestión de productos y servicios,,,,,,,",
            "ELVIS ALBERTO CONDE JIMENEZ,,,,,,,",
            "72213179,,,,,,,",
            "Tipo,Código,Nombre,Unidad,Precios,Impuestos,Stock,Estado",
            "Producto,78762,ACEITE ALMENDRAS,unidad,65000,IVA 19%,-1,Active",
            "Producto,7762,PEEL BOOSTER,unidad,85000,IVA 0%,0,Active",
            "Servicio,SRV001,MASAJE,unidad,40000,IVA 19%,-1,Active");

        ImportProductsCsvResult result = await useCase.ExecuteAsync(new ImportProductsCsvRequest(csv), TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Imported);
        Assert.Equal(0, result.Skipped);
        Assert.Empty(result.Errors);
        Assert.Equal(3, products.Products.Count);
        Assert.All(products.Products, p => Assert.Equal(0m, p.CurrentStock));
        Assert.Equal(19m, products.Products[0].TaxRate);
        Assert.Equal(0m, products.Products[1].TaxRate);
        IReadOnlyList<Category> allCategories = await categories.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, allCategories.Count);
        Guid productoCategoryId = products.Products[0].CategoryId;
        Assert.Equal(productoCategoryId, products.Products[1].CategoryId);
        Assert.NotEqual(productoCategoryId, products.Products[2].CategoryId);
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

        public Task AddAsync(Category category, CancellationToken cancellationToken = default)
        {
            _categories[category.Id] = category;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Category category, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> HasProductsAsync(Guid categoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            _categories.Remove(id);
            return Task.CompletedTask;
        }
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
