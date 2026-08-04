using POS.Application.DTOs.Products;
using POS.Application.Interfaces;
using POS.Application.UseCases.Categories;
using POS.Application.UseCases.Products;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

/// <summary>
/// A half-filled presentation row used to be dropped without a word, so the form reported a
/// successful save for a presentation that never reached the catalogue. These pin the feedback.
/// </summary>
public sealed class ProductsViewModelPresentationValidationTests
{
    [Fact]
    public async Task SaveAsync_WithAnUntouchedSeededRow_SavesAndSaysThereAreNoPresentations()
    {
        var recorder = new RecordingProductRepository();
        ProductsViewModel viewModel = CreateViewModel(recorder);
        viewModel.Name = "X3";

        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(recorder.LastCreated);
        Assert.Empty(recorder.LastCreated!.Presentations);
        Assert.Contains("se vende solo por unidad", viewModel.Message);
    }

    [Fact]
    public async Task SaveAsync_WithACompletePresentation_ConfirmsWhatWasSaved()
    {
        var recorder = new RecordingProductRepository();
        ProductsViewModel viewModel = CreateViewModel(recorder);
        viewModel.Name = "X3";
        viewModel.Presentations.Clear();
        viewModel.Presentations.Add(new PresentationRowViewModel("Paquete de 3", 3, 15_000m));

        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        Assert.Single(recorder.LastCreated!.Presentations);
        Assert.Contains("Paquete de 3 x3", viewModel.Message);
    }

    [Theory]
    [InlineData("Paquete de 3", null, 15_000, "unidades")]
    [InlineData("Paquete de 3", 1, 15_000, "al menos 2 unidades")]
    [InlineData("Paquete de 3", 3, null, "precio")]
    [InlineData("", 3, 15_000, "nombre")]
    public async Task SaveAsync_WithAnIncompletePresentation_StopsAndExplainsWhy(
        string name, int? units, int? price, string expectedHint)
    {
        var recorder = new RecordingProductRepository();
        ProductsViewModel viewModel = CreateViewModel(recorder);
        viewModel.Name = "X3";
        viewModel.Presentations.Clear();
        viewModel.Presentations.Add(new PresentationRowViewModel { Name = name, Units = units, Price = (decimal?)price });

        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        Assert.Null(recorder.LastCreated);
        Assert.Contains("No se guardó", viewModel.Message);
        Assert.Contains(expectedHint, viewModel.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenASavedPresentationHasItsFiguresCleared_StopsInsteadOfDroppingIt()
    {
        var recorder = new RecordingProductRepository();
        ProductsViewModel viewModel = CreateViewModel(recorder);
        viewModel.Name = "X3";
        viewModel.Presentations.Clear();

        // A real presentation loaded from the catalogue whose Units and Price textboxes were emptied.
        // It carries the default name, so a name-based heuristic would mistake it for a blank row.
        viewModel.Presentations.Add(new PresentationRowViewModel { Name = "Caja", Units = null, Price = null });

        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        Assert.Null(recorder.LastCreated);
        Assert.Contains("No se guardó", viewModel.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenARowIsNamedLikeTheSeedInAnotherCase_IsStillValidated()
    {
        var recorder = new RecordingProductRepository();
        ProductsViewModel viewModel = CreateViewModel(recorder);
        viewModel.Name = "X3";
        viewModel.Presentations.Clear();
        viewModel.Presentations.Add(new PresentationRowViewModel { Name = "caja", Units = null, Price = null });

        await viewModel.SaveAsync(TestContext.Current.CancellationToken);

        Assert.Null(recorder.LastCreated);
        Assert.Contains("No se guardó", viewModel.Message);
    }

    private static ProductsViewModel CreateViewModel(RecordingProductRepository products)
    {
        Category category = new("General");
        var categories = new StubCategoryRepository(category);
        var viewModel = new ProductsViewModel(
            new CreateProductUseCase(products, categories, new NoOpUnitOfWork()),
            new UpdateProductUseCase(products, categories, new NoOpUnitOfWork()),
            new DeleteProductUseCase(products, new NoOpUnitOfWork()),
            new SearchProductsUseCase(new EmptyCatalogQueryService()),
            new SearchCategoriesUseCase(categories));
        viewModel.CategoryId = category.Id;
        return viewModel;
    }

    private sealed class RecordingProductRepository : IProductRepository
    {
        public Product? LastCreated { get; private set; }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            LastCreated = product;
            return Task.CompletedTask;
        }
    }

    private sealed class StubCategoryRepository : ICategoryRepository
    {
        private readonly Category _category;

        public StubCategoryRepository(Category category) => _category = category;

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Category?>(id == _category.Id ? _category : null);

        public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Category>>([_category]);

        public Task AddAsync(Category category, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> HasProductsAsync(Guid categoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task DeleteAsync(Guid categoryId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class EmptyCatalogQueryService : IProductCatalogQueryService
    {
        public Task<IReadOnlyList<ProductSearchResult>> SearchAsync(SearchProductsRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProductSearchResult>>([]);
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
    }
}
