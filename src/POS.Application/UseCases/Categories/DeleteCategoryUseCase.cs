using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Categories;

public sealed class DeleteCategoryUseCase
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCategoryUseCase(ICategoryRepository categories, IUnitOfWork unitOfWork)
    {
        _categories = categories ?? throw new ArgumentNullException(nameof(categories));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task ExecuteAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false);
        if (category == null)
        {
            throw new InvalidOperationException("Category was not found.");
        }

        if (await _categories.HasProductsAsync(categoryId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Cannot delete category with associated active products.");
        }

        await _categories.DeleteAsync(categoryId, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
