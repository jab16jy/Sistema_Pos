using POS.Application.UseCases.Auth;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Roles;

public sealed class CreateRoleUseCase
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _unitOfWork;

    public CreateRoleUseCase(IUserRepository users, IRoleRepository roles, IUnitOfWork unitOfWork)
        => (_users, _roles, _unitOfWork) = (users, roles, unitOfWork);

    public async Task<(bool Success, string? Error)> ExecuteAsync(
        string name, IReadOnlySet<string> permissionKeys, Guid requestingUserId, CancellationToken ct = default)
    {
        var authorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
            _users,
            _roles,
            requestingUserId,
            PermissionKeys.Roles.Create,
            "No tienes permiso para crear roles.",
            ct).ConfigureAwait(false);
        if (authorizationError is not null)
            return (false, authorizationError);

        if (permissionKeys.Count > 0)
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

        if (string.IsNullOrWhiteSpace(name))
            return (false, "El nombre del rol es requerido.");

        var normalizedName = name.Trim();
        if (await _roles.GetByNameAsync(normalizedName, ct).ConfigureAwait(false) is not null)
            return (false, "Ya existe un rol con ese nombre.");

        if (permissionKeys.Any(key => !PermissionKeys.All.Contains(key, StringComparer.Ordinal)))
            return (false, "Uno o más permisos no son válidos.");

        var role = new Role(normalizedName);
        await _roles.AddAsync(role, ct).ConfigureAwait(false);
        await _roles.ReplacePermissionsAsync(role.Id, permissionKeys, ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        return (true, null);
    }
}
