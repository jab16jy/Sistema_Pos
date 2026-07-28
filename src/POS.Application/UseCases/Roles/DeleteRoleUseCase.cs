using POS.Application.UseCases.Auth;
using POS.Domain.Authorization;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Roles;

public sealed class DeleteRoleUseCase
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRoleUseCase(IUserRepository users, IRoleRepository roles, IUnitOfWork unitOfWork)
        => (_users, _roles, _unitOfWork) = (users, roles, unitOfWork);

    public async Task<(bool Success, string? Error)> ExecuteAsync(
        Guid roleId, Guid requestingUserId, CancellationToken ct = default)
    {
        var authorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
            _users,
            _roles,
            requestingUserId,
            PermissionKeys.Roles.Deactivate,
            "No tienes permiso para eliminar roles.",
            ct).ConfigureAwait(false);
        if (authorizationError is not null)
            return (false, authorizationError);

        var role = await _roles.GetByIdAsync(roleId, ct).ConfigureAwait(false);
        if (role is null)
            return (false, "Rol no encontrado.");

        if (role.IsSystem)
            return (false, "Los roles del sistema no se pueden eliminar.");

        if (await _roles.CountUsersInRoleAsync(roleId, ct).ConfigureAwait(false) > 0)
            return (false, "No puedes eliminar un rol asignado a usuarios.");

        await _roles.DeleteAsync(role, ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        return (true, null);
    }
}
