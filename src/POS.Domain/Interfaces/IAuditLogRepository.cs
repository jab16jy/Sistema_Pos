using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int limit = 100, CancellationToken cancellationToken = default);
}
