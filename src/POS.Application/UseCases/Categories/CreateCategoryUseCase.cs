using POS.Application.DTOs.Products;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Categories;

public sealed class CreateCategoryUseCase
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCategoryUseCase(ICategoryRepository categories, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<CategoryResult> ExecuteAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Category name is required.", nameof(request));
        }

        // Enforce unique name (case-insensitive)
        var existing = await _categories.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (existing.Any(c => string.Equals(c.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A category named '{request.Name.Trim()}' already exists.");
        }

        var category = new Category(request.Name);
        if (!request.IsActive)
        {
            category.Deactivate();
        }
        await _categories.AddAsync(category, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CategoryResult(category.Id, category.Name, category.IsActive);
    }
}
