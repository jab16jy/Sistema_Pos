using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);
    Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default);
    Task<bool> HasPurchasesAsync(Guid id, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
