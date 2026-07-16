using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class CashierShiftRepository : ICashierShiftRepository
{
    private readonly AppDbContext _dbContext;

    public CashierShiftRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
    {
        return _dbContext.CashierShifts
            .FirstOrDefaultAsync(s => s.CashierUserId == cashierUserId && s.Status == ShiftStatus.Open, cancellationToken);
    }

    public Task<CashierShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.CashierShifts.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CashierShift>> GetRecentAsync(
        int limit = 50,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return [];
        }

        int pageSize = Math.Min(limit, 500);

        var query = _dbContext.CashierShifts.AsQueryable();

        if (fromDate.HasValue)
        {
            query = query.Where(s => s.OpenedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(s => s.OpenedAt < toDate.Value);
        }

        return await query
            .OrderByDescending(s => s.OpenedAt)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(CashierShift shift, CancellationToken cancellationToken = default)
    {
        await _dbContext.CashierShifts.AddAsync(shift, cancellationToken).ConfigureAwait(false);
    }
}
