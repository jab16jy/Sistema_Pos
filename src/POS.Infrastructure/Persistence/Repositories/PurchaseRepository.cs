using POS.Domain.Entities;
using POS.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

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

    public Task<Purchase?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Purchases.Include(purchase => purchase.Items).FirstOrDefaultAsync(purchase => purchase.Id == id, cancellationToken);
}
