using POS.Application.DTOs.Auth;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed class LoginUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHashService _hasher;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;

    public LoginUseCase(IUserRepository users, IPasswordHashService hasher, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork)
    {
        _users = users;
        _hasher = hasher;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<LoginResult> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return new LoginResult(false, "Usuario y contraseña son requeridos.");

        string username = request.Username.Trim();
        var user = await _users.GetByUsernameAsync(username, ct);
        if (user is null || !user.IsActive)
            return await AuditFailureAsync(username, "Usuario no encontrado o inactivo.", ct);

        if (!_hasher.VerifyPassword(request.Password, user.PasswordHash))
            return await AuditFailureAsync(username, "Contraseña incorrecta.", ct, user.Id);

        await _auditLogs.AddAsync(new AuditLog(user.Id, user.Username, "Login", "User", user.Username), ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        return new LoginResult(true, null, user.Id, user.Username, user.Role);
    }

    private async Task<LoginResult> AuditFailureAsync(string attemptedUsername, string errorMessage, CancellationToken ct, Guid? userId = null)
    {
        await _auditLogs.AddAsync(new AuditLog(userId, attemptedUsername, "LoginFailed", "User", attemptedUsername, errorMessage), ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        return new LoginResult(false, errorMessage);
    }
}
