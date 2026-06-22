using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class PurchaseRepository : IPurchaseRepository
{
    private readonly AppDbContext _dbContext;

    public PurchaseRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Purchase purchase, IReadOnlyCollection<InventoryMovement> movements, CancellationToken cancellationToken = default)
    {
        await _dbContext.Purchases.AddAsync(purchase, cancellationToken);
        await _dbContext.InventoryMovements.AddRangeAsync(movements, cancellationToken);
    }
}
