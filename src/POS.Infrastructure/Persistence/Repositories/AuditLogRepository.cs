using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _dbContext;

    public AuditLogRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
        => await _dbContext.AuditLogs.AddAsync(auditLog, cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(
        int limit = 100,
        AuditLogFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        int pageSize = Math.Clamp(limit, 1, 500);
        int page = filter is null ? 1 : Math.Max(1, filter.Page);

        // Real SQL WHERE + ORDER BY + Skip/Take: AuditLog.CreatedAt now has the same INTEGER
        // conversion as Sale.CreatedAt (see AppDbContext.OnModelCreating), which is what makes
        // ORDER BY translatable for SQLite — without it EF throws NotSupportedException and this
        // whole query would have to fall back to loading the table into memory to sort client-side.
        IQueryable<AuditLog> query = _dbContext.AuditLogs.AsNoTracking();

        if (filter is { HasFilters: true })
        {
            if (!string.IsNullOrWhiteSpace(filter.Username))
            {
                string usernamePattern = $"%{filter.Username.Trim()}%";
                query = query.Where(log => EF.Functions.Like(log.Username, usernamePattern));
            }

            if (!string.IsNullOrWhiteSpace(filter.Action))
            {
                string action = filter.Action.Trim();
                query = query.Where(log => log.Action == action);
            }

            if (!string.IsNullOrWhiteSpace(filter.EntityName))
            {
                string entityName = filter.EntityName.Trim();
                query = query.Where(log => log.EntityName == entityName);
            }
        }

        return await query
            .OrderByDescending(log => log.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
