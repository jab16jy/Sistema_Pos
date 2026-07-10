using POS.Application.DTOs.Products;
using POS.Application.UseCases.Categories;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class CategoriesViewModelTests
{
    [Fact]
    public async Task LoadAsync_WhenRepositoryFails_SetsRecoverableMessage()
    {
        var repository = new ThrowingCategoryRepository();
        var viewModel = new CategoriesViewModel(
            new SearchCategoriesUseCase(repository),
            new CreateCategoryUseCase(repository, new NoOpUnitOfWork()),
            new UpdateCategoryUseCase(repository, new NoOpUnitOfWork()),
            new DeleteCategoryUseCase(repository, new NoOpUnitOfWork()));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar categorias", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeleteCommand_CanExecute_DependsOnSelectedCategory()
    {
        var repository = new ThrowingCategoryRepository();
        var viewModel = new CategoriesViewModel(
            new SearchCategoriesUseCase(repository),
            new CreateCategoryUseCase(repository, new NoOpUnitOfWork()),
            new UpdateCategoryUseCase(repository, new NoOpUnitOfWork()),
            new DeleteCategoryUseCase(repository, new NoOpUnitOfWork()));

        Assert.False(viewModel.DeleteCommand.CanExecute(null));

        viewModel.SelectedCategory = new CategoryResult(Guid.NewGuid(), "Drinks", true);
        Assert.True(viewModel.DeleteCommand.CanExecute(null));

        viewModel.SelectedCategory = null;
        Assert.False(viewModel.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeleteCommand_Execute_DeletesAndReloadsAndFiresChangedEvent()
    {
        var repository = new InMemoryCategoryRepo();
        var category = new Category("Electronics");
        repository.Categories.Add(category);

        var viewModel = new CategoriesViewModel(
            new SearchCategoriesUseCase(repository),
            new CreateCategoryUseCase(repository, new NoOpUnitOfWork()),
            new UpdateCategoryUseCase(repository, new NoOpUnitOfWork()),
            new DeleteCategoryUseCase(repository, new NoOpUnitOfWork()));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        Assert.Single(viewModel.Categories);

        viewModel.SelectedCategory = viewModel.Categories[0];

        bool eventFired = false;
        viewModel.CategoriesChanged += (s, e) => eventFired = true;

        await viewModel.DeleteAsync(TestContext.Current.CancellationToken);

        Assert.Empty(viewModel.Categories);
        Assert.Null(viewModel.SelectedCategory);
        Assert.True(eventFired);
        Assert.Contains("eliminada exitosamente", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InMemoryCategoryRepo : ICategoryRepository
    {
        public List<Category> Categories { get; } = [];

        public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Category>>(Categories);

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Categories.FirstOrDefault(c => c.Id == id));

        public Task AddAsync(Category category, CancellationToken cancellationToken = default)
        {
            Categories.Add(category);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> HasProductsAsync(Guid categoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var category = Categories.FirstOrDefault(c => c.Id == id);
            if (category != null)
            {
                Categories.Remove(category);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCategoryRepository : ICategoryRepository
    {
        public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falló categorias");

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Category?>(null);

        public Task AddAsync(Category category, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> HasProductsAsync(Guid categoryId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
