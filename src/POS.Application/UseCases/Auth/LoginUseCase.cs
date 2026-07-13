using POS.Application.DTOs.Auth;
using POS.Application.Interfaces;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed class LoginUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHashService _hasher;
    private readonly IAuditLogRepository _auditLogs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRoleRepository _roles;

    public LoginUseCase(IUserRepository users, IRoleRepository roles, IPasswordHashService hasher, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork)
    {
        _users = users;
        _hasher = hasher;
        _auditLogs = auditLogs;
        _unitOfWork = unitOfWork;
        _roles = roles;
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

        var role = await _roles.GetByIdAsync(user.RoleId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("User's role no longer exists.");
        if (!role.IsActive)
            return await AuditFailureAsync(username, "El rol del usuario está inactivo.", ct, user.Id);

        IReadOnlySet<string> permissions = RoleConstants.IsSystemAdministrator(role.Id)
            ? new HashSet<string>(PermissionKeys.All, StringComparer.Ordinal)
            : await _roles.GetPermissionKeysAsync(role.Id, ct).ConfigureAwait(false);

        await _auditLogs.AddAsync(new AuditLog(user.Id, user.Username, "Login", "User", user.Username), ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        return new LoginResult(true, null, user.Id, user.Username, role.Id, role.Name, permissions);
    }

    private async Task<LoginResult> AuditFailureAsync(string attemptedUsername, string errorMessage, CancellationToken ct, Guid? userId = null)
    {
        await _auditLogs.AddAsync(new AuditLog(userId, attemptedUsername, "LoginFailed", "User", attemptedUsername, errorMessage), ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        return new LoginResult(false, errorMessage);
    }
}
