using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class ReceivableRepository : IReceivableRepository
{
    private readonly AppDbContext _dbContext;

    public ReceivableRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Receivable receivable, CancellationToken cancellationToken = default)
    {
        await _dbContext.Receivables.AddAsync(receivable, cancellationToken);
    }

    public async Task<Receivable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Receivables
            .Include(r => r.Settlements)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Receivable>> GetOpenByCustomerIdAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var receivables = await _dbContext.Receivables
            .Include(r => r.Settlements)
            .Where(r => r.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        return receivables
            .Where(r => !r.IsCancelled && r.OutstandingBalance.Amount > 0m)
            .OrderBy(r => r.CreatedAt)
            .ToList();
    }

    public async Task<Receivable?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Receivables
            .Include(r => r.Settlements)
            .FirstOrDefaultAsync(r => r.SaleId == saleId, cancellationToken);
    }

    public async Task UpdateAsync(Receivable receivable, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Entry(receivable).State == EntityState.Detached)
        {
            _dbContext.Receivables.Update(receivable);
        }

        foreach (ReceivableSettlement settlement in receivable.Settlements)
        {
            EntityEntry settlementEntry = _dbContext.Entry(settlement);
            if (settlementEntry.State == EntityState.Detached ||
                !await _dbContext.ReceivableSettlements.AnyAsync(
                    existing => existing.Id == settlement.Id,
                    cancellationToken).ConfigureAwait(false))
            {
                settlementEntry.State = EntityState.Added;
            }
        }

    }

    public async Task AddReturnSettlementAsync(ReturnSettlement settlement, CancellationToken cancellationToken = default)
    {
        await _dbContext.ReturnSettlements.AddAsync(settlement, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddReturnReceivableReconciliationAsync(
        ReturnReceivableReconciliation reconciliation,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.ReturnReceivableReconciliations.AddAsync(reconciliation, cancellationToken).ConfigureAwait(false);
    }
}
