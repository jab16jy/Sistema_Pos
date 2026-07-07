using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _dbContext;

    public AuditLogRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        // EF Core's SQLite provider can't translate ORDER BY over a raw DateTimeOffset into SQL
        // (throws NotSupportedException). Ordering client-side avoids that without adding a
        // DB-level int conversion, which would silently misread already-persisted TEXT timestamps
        // as garbage dates instead of erroring.
        // ponytail: fetches the whole table; add a date-bounded WHERE if AUDIT_LOGS grows large.
        List<AuditLog> logs = await _dbContext.AuditLogs
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return logs
            .OrderByDescending(log => log.CreatedAt)
            .Take(limit)
            .ToList();
    }
}
