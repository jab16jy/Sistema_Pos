using POS.Application.UseCases.Auth;
using POS.Domain.Authorization;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Roles;

public sealed class UpdateRoleUseCase
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRoleUseCase(IUserRepository users, IRoleRepository roles, IUnitOfWork unitOfWork)
        => (_users, _roles, _unitOfWork) = (users, roles, unitOfWork);

    public async Task<(bool Success, string? Error)> ExecuteAsync(
        Guid roleId, string newName, IReadOnlySet<string> permissionKeys, Guid requestingUserId, CancellationToken ct = default)
    {
        var authorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
            _users,
            _roles,
            requestingUserId,
            PermissionKeys.Roles.Edit,
            "No tienes permiso para actualizar roles.",
            ct).ConfigureAwait(false);
        if (authorizationError is not null)
            return (false, authorizationError);

        var role = await _roles.GetByIdAsync(roleId, ct).ConfigureAwait(false);
        if (role is null)
            return (false, "Rol no encontrado.");

        IReadOnlySet<string> currentPermissionKeys = await _roles.GetPermissionKeysAsync(roleId, ct).ConfigureAwait(false);
        var currentPermissions = new HashSet<string>(currentPermissionKeys, StringComparer.Ordinal);
        bool permissionsChanged = !currentPermissions.SetEquals(permissionKeys);

        if (permissionsChanged)
        {
            authorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
                _users,
                _roles,
                requestingUserId,
                PermissionKeys.Roles.AssignPermissions,
                "No tienes permiso para asignar permisos a roles.",
                ct).ConfigureAwait(false);
            if (authorizationError is not null)
                return (false, authorizationError);
        }

        if (RoleConstants.IsSystemAdministrator(roleId) && permissionsChanged)
            return (false, "Los permisos del rol Administrador no se pueden modificar.");

        if (string.IsNullOrWhiteSpace(newName))
            return (false, "El nombre del rol es requerido.");

        var normalizedName = newName.Trim();
        var existing = await _roles.GetByNameAsync(normalizedName, ct).ConfigureAwait(false);
        if (existing is not null && existing.Id != roleId)
            return (false, "Ya existe un rol con ese nombre.");

        if (permissionKeys.Any(key => !PermissionKeys.All.Contains(key, StringComparer.Ordinal)))
            return (false, "Uno o más permisos no son válidos.");

        role.Rename(normalizedName);
        await _roles.UpdateAsync(role, ct).ConfigureAwait(false);
        if (permissionsChanged)
            await _roles.ReplacePermissionsAsync(roleId, permissionKeys, ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        return (true, null);
    }
}
