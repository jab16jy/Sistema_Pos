using POS.Application.Interfaces;
using POS.Domain.Authorization;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed class UpdateUserUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHashService _hasher;
    private readonly IRoleRepository _roles;

    public UpdateUserUseCase(IUserRepository users, IRoleRepository roles, IPasswordHashService hasher)
    {
        _users = users;
        _hasher = hasher;
        _roles = roles;
    }

    public async Task<(bool Success, string? Error)> ExecuteAsync(
        Guid userId, string? newPassword, Guid newRoleId, Guid requestingUserId, CancellationToken ct = default)
    {
        var authorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
            _users,
            _roles,
            requestingUserId,
            PermissionKeys.Users.Edit,
            "No tienes permiso para actualizar usuarios.",
            ct).ConfigureAwait(false);
        if (authorizationError is not null)
            return (false, authorizationError);

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null) return (false, "Usuario no encontrado.");

        if (newRoleId == RoleConstants.AdministratorRoleId || RoleConstants.IsSystemAdministrator(user.RoleId))
        {
            var requester = await _users.GetByIdAsync(requestingUserId, ct).ConfigureAwait(false);
            if (requester is null || !RoleConstants.IsSystemAdministrator(requester.RoleId))
            {
                return (false, "No tienes permiso para asignar o modificar el rol de Administrador.");
            }
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword.Length < 4)
                return (false, "La contraseña debe tener al menos 4 caracteres.");
            user.UpdatePasswordHash(_hasher.HashPassword(newPassword));
        }

        user.ChangeRole(newRoleId);
        await _users.UpdateAsync(user, ct);
        return (true, null);
    }
}
