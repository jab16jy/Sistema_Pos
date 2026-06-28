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
        return await _dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(log => log.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
