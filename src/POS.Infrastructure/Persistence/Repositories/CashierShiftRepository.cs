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

    public async Task<IReadOnlyList<CashierShift>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
    {
        var shifts = await _dbContext.CashierShifts
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return shifts
            .OrderByDescending(s => s.OpenedAt)
            .Take(limit)
            .ToList();
    }

    public async Task AddAsync(CashierShift shift, CancellationToken cancellationToken = default)
    {
        await _dbContext.CashierShifts.AddAsync(shift, cancellationToken).ConfigureAwait(false);
    }
}
