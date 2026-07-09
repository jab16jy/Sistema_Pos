using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(
        int limit = 100,
        AuditLogFilter? filter = null,
        CancellationToken cancellationToken = default);

    // ponytail: only login/logout write here explicitly — every other entity is audited automatically
    // by AppDbContext.SaveChangesAsync via the ChangeTracker interceptor, which can't see auth events
    // because they don't mutate a tracked entity.
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
}
