using POS.Application.Interfaces;
using POS.Domain.Authorization;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed class UpdateUserUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHashService _hasher;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUserUseCase(IUserRepository users, IRoleRepository roles, IPasswordHashService hasher, IUnitOfWork unitOfWork)
    {
        _users = users;
        _hasher = hasher;
        _roles = roles;
        _unitOfWork = unitOfWork;
    }

    public async Task<(bool Success, string? Error)> ExecuteAsync(
        Guid userId, string? newPassword, Guid newRoleId, Guid requestingUserId, CancellationToken ct = default)
    {
        (bool Success, string? Error) result = default;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
            {
                var authorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
                    _users,
                    _roles,
                    requestingUserId,
                    PermissionKeys.Users.Edit,
                    "No tienes permiso para actualizar usuarios.",
                    transactionCt).ConfigureAwait(false);
                if (authorizationError is not null)
                {
                    result = (false, authorizationError);
                    return;
                }

                var user = await _users.GetByIdAsync(userId, transactionCt).ConfigureAwait(false);
                if (user is null)
                {
                    result = (false, "Usuario no encontrado.");
                    return;
                }

                if (newRoleId == RoleConstants.AdministratorRoleId || RoleConstants.IsSystemAdministrator(user.RoleId))
                {
                    var requester = await _users.GetByIdAsync(requestingUserId, transactionCt).ConfigureAwait(false);
                    if (requester is null || !RoleConstants.IsSystemAdministrator(requester.RoleId))
                    {
                        result = (false, "No tienes permiso para asignar o modificar el rol de Administrador.");
                        return;
                    }
                }

                if (newRoleId != user.RoleId)
                {
                    var roleAssignmentAuthorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
                        _users,
                        _roles,
                        requestingUserId,
                        PermissionKeys.Roles.AssignPermissions,
                        "No tienes permiso para asignar roles.",
                        transactionCt).ConfigureAwait(false);
                    if (roleAssignmentAuthorizationError is not null)
                    {
                        result = (false, roleAssignmentAuthorizationError);
                        return;
                    }
                }

                if (user.IsActive
                    && RoleConstants.IsSystemAdministrator(user.RoleId)
                    && newRoleId != RoleConstants.AdministratorRoleId
                    && await _users.CountActiveUsersInRoleAsync(RoleConstants.AdministratorRoleId, transactionCt).ConfigureAwait(false) <= 1)
                {
                    result = (false, "No puedes quitar el rol de administrador al último administrador activo.");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(newPassword))
                {
                    if (newPassword.Length < 4)
                    {
                        result = (false, "La contraseña debe tener al menos 4 caracteres.");
                        return;
                    }

                    user.UpdatePasswordHash(_hasher.HashPassword(newPassword));
                }

                user.ChangeRole(newRoleId);
                await _users.UpdateAsync(user, transactionCt).ConfigureAwait(false);
                result = (true, null);
            }, ct).ConfigureAwait(false);
        }
        catch (TransactionConflictException)
        {
            return (false, "La base de datos está ocupada. Inténtalo de nuevo.");
        }

        return result;
    }
}
