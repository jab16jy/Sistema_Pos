using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class InventoryRepository : IInventoryRepository
{
    private readonly AppDbContext _dbContext;

    public InventoryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive)
            .OrderBy(product => product.Name)
            .Select(product => new InventoryStockLevel(
                product.Id,
                product.Name,
                product.Barcode,
                product.CurrentStock,
                product.MinimumStock,
                product.MinimumStock > 0m &&
                product.CurrentStock > 0m &&
                product.CurrentStock <= product.MinimumStock))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        // SQLite can't ORDER BY DateTimeOffset — sort client-side.
        List<InventoryMovement> movements = await _dbContext.InventoryMovements
            .AsNoTracking()
            .Where(movement => movement.ProductId == productId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return movements
            .OrderByDescending(movement => movement.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive && product.MinimumStock > 0m && product.CurrentStock <= product.MinimumStock)
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default)
    {
        await _dbContext.InventoryMovements.AddAsync(movement, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<InventoryMovement>> GetMovementsByReferenceIdAsync(
        Guid referenceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.InventoryMovements
            .Where(m => m.ReferenceId == referenceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
