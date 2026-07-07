using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(
        int limit = 100,
        AuditLogFilter? filter = null,
        CancellationToken cancellationToken = default);
}
