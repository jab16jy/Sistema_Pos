using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

// ponytail: logout has no entity mutation for the interceptor in AppDbContext to pick up, same as login;
// write the audit entry explicitly instead of inventing a session-lifecycle event bus.
public sealed class LogoutUseCase
{
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;

    public LogoutUseCase(IAuditLogRepository auditLogs, IUnitOfWork unitOfWork)
    {
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(Guid userId, string username, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(username))
            return;

        await _auditLogs.AddAsync(new AuditLog(userId, username, "Logout", "User", username), ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
