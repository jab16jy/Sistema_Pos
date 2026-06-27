using POS.Application.DTOs.Products;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Categories;

public sealed class SearchCategoriesUseCase
{
    private readonly ICategoryRepository _categories;

    public SearchCategoriesUseCase(ICategoryRepository categories)
    {
        _categories = categories;
    }

    public async Task<IReadOnlyList<CategoryResult>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _categories.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return categories
            .Select(c => new CategoryResult(c.Id, c.Name, c.IsActive))
            .ToList()
            .AsReadOnly();
    }
}
