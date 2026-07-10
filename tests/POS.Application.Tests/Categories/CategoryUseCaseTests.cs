using POS.Application.DTOs.Products;
using POS.Application.UseCases.Categories;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using Xunit;

namespace POS.Application.Tests.Categories;

public sealed class CategoryUseCaseTests
{
    [Fact]
    public async Task CreateCategory_WithValidName_ReturnsCategoryResult()
    {
        var repo = new InMemCategoryRepository();
        var uow = new CountingUow();
        var useCase = new CreateCategoryUseCase(repo, uow);

        var result = await useCase.ExecuteAsync(new CreateCategoryRequest("Electronics"), TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Electronics", result.Name);
        Assert.True(result.IsActive);
        Assert.Single(repo.Categories);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateName_Throws()
    {
        var repo = new InMemCategoryRepository();
        repo.Categories.Add(new Category("Electronics"));
        var uow = new CountingUow();
        var useCase = new CreateCategoryUseCase(repo, uow);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(new CreateCategoryRequest("electronics"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteCategory_WhenCategoryDoesNotExist_Throws()
    {
        var repo = new InMemCategoryRepository();
        var uow = new CountingUow();
        var useCase = new DeleteCategoryUseCase(repo, uow);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteCategory_WhenProductsExist_Throws()
    {
        var repo = new InMemCategoryRepository();
        var category = new Category("Tools");
        repo.Categories.Add(category);
        repo.CategoryHasProducts[category.Id] = true;
        var uow = new CountingUow();
        var useCase = new DeleteCategoryUseCase(repo, uow);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(category.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteCategory_WhenNoProductsExist_RemovesCategoryAndSaves()
    {
        var repo = new InMemCategoryRepository();
        var category = new Category("Tools");
        repo.Categories.Add(category);
        repo.CategoryHasProducts[category.Id] = false;
        var uow = new CountingUow();
        var useCase = new DeleteCategoryUseCase(repo, uow);

        await useCase.ExecuteAsync(category.Id, TestContext.Current.CancellationToken);

        Assert.Empty(repo.Categories);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task SearchCategories_ReturnsMappedResults()
    {
        var repo = new InMemCategoryRepository();
        repo.Categories.Add(new Category("Beverages"));
        repo.Categories.Add(new Category("Snacks"));
        var useCase = new SearchCategoriesUseCase(repo);

        var results = await useCase.ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Name == "Beverages");
        Assert.Contains(results, r => r.Name == "Snacks");
    }

    // --- Stubs ---

    private sealed class InMemCategoryRepository : ICategoryRepository
    {
        public List<Category> Categories { get; } = [];
        public Dictionary<Guid, bool> CategoryHasProducts { get; } = [];

        public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Category>>(Categories);

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Categories.FirstOrDefault(c => c.Id == id));

        public Task AddAsync(Category category, CancellationToken ct = default)
        {
            Categories.Add(category);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Category category, CancellationToken ct = default) => Task.CompletedTask;

        public Task<bool> HasProductsAsync(Guid categoryId, CancellationToken ct = default)
            => Task.FromResult(CategoryHasProducts.GetValueOrDefault(categoryId, false));

        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            var category = Categories.FirstOrDefault(c => c.Id == id);
            if (category != null)
            {
                Categories.Remove(category);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class CountingUow : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
