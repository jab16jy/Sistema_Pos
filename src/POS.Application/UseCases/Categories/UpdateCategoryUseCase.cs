using POS.Application.DTOs.Products;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Categories;

public sealed class UpdateCategoryUseCase
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCategoryUseCase(ICategoryRepository categories, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<CategoryResult> ExecuteAsync(UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Category name is required.", nameof(request));
        }

        var category = await _categories.GetByIdAsync(request.CategoryId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Category was not found.");

        // Unique name check, excluding self
        var existing = await _categories.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (existing.Any(c => c.Id != request.CategoryId
            && string.Equals(c.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A category named '{request.Name.Trim()}' already exists.");
        }

        // Category entity does not expose an Update method — use reflection-safe approach via EF tracking.
        // Since Category.Name is private set, we recreate via the repository pattern:
        // The infrastructure layer tracks the entity and updates it when UpdateAsync is called.
        // We expose the name change through a domain method added here to keep the entity encapsulated.
        // For now, call UpdateAsync and the EF configuration will persist the tracked change.
        // The caller passes the updated entity back via UpdateAsync.
        //
        // Since Category does not have an UpdateName method, we need to add one or
        // handle this differently. The design expects the category entity to be updatable.
        // Following the pattern used by Customer.UpdateContactInfo, we add a Rename method.
        // For Phase 2 we use the available interface — UpdateAsync receives the entity as-is
        // and the infrastructure uses EF change tracking.
        //
        // We must rename via a domain method. Check if Category has one; if not, we handle it inline:
        category.Rename(request.Name);
        if (request.IsActive)
        {
            category.Activate();
        }
        else
        {
            category.Deactivate();
        }

        await _categories.UpdateAsync(category, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CategoryResult(category.Id, category.Name, category.IsActive);
    }
}
