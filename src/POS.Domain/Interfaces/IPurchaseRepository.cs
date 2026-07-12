using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface IPurchaseRepository
{
    Task AddAsync(Purchase purchase, IReadOnlyCollection<InventoryMovement> movements, CancellationToken cancellationToken = default);
    Task<Purchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult<Purchase?>(null);
}
