using POS.Application.DTOs.Audit;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Audit;

public sealed class GetAuditLogsUseCase
{
    private readonly IAuditLogRepository _auditLogs;

    public GetAuditLogsUseCase(IAuditLogRepository auditLogs) => _auditLogs = auditLogs;

    public async Task<IReadOnlyList<AuditLogEntry>> ExecuteAsync(
        int limit = 100,
        AuditLogFilter? filter = null,
        CancellationToken ct = default)
    {
        var logs = await _auditLogs.GetRecentAsync(limit, filter, ct).ConfigureAwait(false);
        return logs.Select(log => new AuditLogEntry(
            log.Id,
            log.UserId,
            log.Username,
            log.Action,
            log.EntityName,
            log.EntityId,
            log.Details,
            log.CreatedAt)).ToList();
    }
}
