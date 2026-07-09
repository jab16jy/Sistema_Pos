using POS.Application.DTOs.Audit;
using POS.Application.UseCases.Auth;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Audit;

public sealed class GetAuditLogsUseCase
{
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUserRepository _users;

    public GetAuditLogsUseCase(IAuditLogRepository auditLogs, IUserRepository users)
    {
        _auditLogs = auditLogs;
        _users = users;
    }

    public async Task<IReadOnlyList<AuditLogEntry>> ExecuteAsync(
        Guid requestingUserId,
        int limit = 100,
        AuditLogFilter? filter = null,
        CancellationToken ct = default)
    {
        // Restriction used to live only in MainShellViewModel's UI gating — enforce it here too so
        // any caller with the DI container (not just the admin hub) is denied read access.
        string? unauthorized = await AdminUserAuthorization.EnsureAdministratorAsync(
            _users, requestingUserId, "Solo un administrador puede consultar la auditoría.", ct).ConfigureAwait(false);
        if (unauthorized is not null)
        {
            throw new UnauthorizedActionException("GetAuditLogs");
        }

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
