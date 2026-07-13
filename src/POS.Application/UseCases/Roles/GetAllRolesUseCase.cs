using POS.Domain.Authorization;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using POS.Application.UseCases.Auth;

namespace POS.Application.UseCases.Roles;

public sealed record RoleSummary(Guid Id, string Name, bool IsSystem, bool IsActive);

public sealed class GetAllRolesUseCase
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;

    public GetAllRolesUseCase(IUserRepository users, IRoleRepository roles) => (_users, _roles) = (users, roles);

    public async Task<IReadOnlyList<RoleSummary>> ExecuteAsync(Guid requestingUserId, CancellationToken ct = default)
    {
        var authorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
            _users,
            _roles,
            requestingUserId,
            PermissionKeys.Roles.View,
            "No tienes permiso para consultar roles.",
            ct).ConfigureAwait(false);
        if (authorizationError is not null)
            throw new UnauthorizedActionException("GetAllRoles");

        var roles = await _roles.GetAllAsync(ct).ConfigureAwait(false);
        return roles.Select(role => new RoleSummary(role.Id, role.Name, role.IsSystem, role.IsActive)).ToList();
    }
}
