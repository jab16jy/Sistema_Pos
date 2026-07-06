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
            new UpdateCategoryUseCase(repository, new NoOpUnitOfWork()));

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar categorias", viewModel.Message, StringComparison.OrdinalIgnoreCase);
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
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }
}
