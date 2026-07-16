using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface ICashierShiftRepository
{
    Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default);
    Task<CashierShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashierShift>> GetRecentAsync(int limit = 50, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default);
    Task AddAsync(CashierShift shift, CancellationToken cancellationToken = default);
}
