using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Category category, CancellationToken cancellationToken = default);
    Task UpdateAsync(Category category, CancellationToken cancellationToken = default);

    /// <summary>Returns true if any active product references this category.</summary>
    Task<bool> HasProductsAsync(Guid categoryId, CancellationToken cancellationToken = default);
}
