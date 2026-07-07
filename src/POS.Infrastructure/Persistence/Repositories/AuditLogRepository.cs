using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _dbContext;

    public AuditLogRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(
        int limit = 100,
        AuditLogFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        // EF Core's SQLite provider can't translate ORDER BY over a raw DateTimeOffset into SQL
        // (throws NotSupportedException). Ordering client-side avoids that without adding a
        // DB-level int conversion, which would silently misread already-persisted TEXT timestamps
        // as garbage dates instead of erroring.
        // Do not switch this to an INTEGER conversion without a migration/backfill.
        // ponytail: fetches the whole table; add a date-bounded WHERE if AUDIT_LOGS grows large.
        List<AuditLog> logs = await _dbContext.AuditLogs
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IEnumerable<AuditLog> query = logs;
        if (filter is { HasFilters: true })
        {
            query = query.Where(log => MatchesFilter(log, filter));
        }

        return query
            .OrderByDescending(log => log.CreatedAt)
            .Take(limit)
            .ToList();
    }

    private static bool MatchesFilter(AuditLog log, AuditLogFilter filter)
    {
        return MatchesContains(log.Username, filter.Username) &&
            MatchesExact(log.Action, filter.Action) &&
            MatchesExact(log.EntityName, filter.EntityName);
    }

    private static bool MatchesContains(string value, string? filter)
        => string.IsNullOrWhiteSpace(filter) || value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);

    private static bool MatchesExact(string value, string? filter)
        => string.IsNullOrWhiteSpace(filter) || string.Equals(value, filter.Trim(), StringComparison.OrdinalIgnoreCase);
}
