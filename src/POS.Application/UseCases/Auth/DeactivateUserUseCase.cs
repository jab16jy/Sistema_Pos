using POS.Domain.Authorization;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed class DeactivateUserUseCase
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateUserUseCase(IUserRepository users, IRoleRepository roles, IUnitOfWork unitOfWork)
        => (_users, _roles, _unitOfWork) = (users, roles, unitOfWork);

    public async Task<(bool Success, string? Error)> ExecuteAsync(
        Guid userId, Guid requestingUserId, CancellationToken ct = default)
    {
        if (userId == requestingUserId)
            return (false, "No puedes desactivar tu propio usuario.");

        (bool Success, string? Error) result = default;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
            {
                var authorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
                    _users,
                    _roles,
                    requestingUserId,
                    PermissionKeys.Users.Deactivate,
                    "No tienes permiso para desactivar usuarios.",
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

                if (user.IsActive
                    && RoleConstants.IsSystemAdministrator(user.RoleId)
                    && await _users.CountActiveUsersInRoleAsync(RoleConstants.AdministratorRoleId, transactionCt).ConfigureAwait(false) <= 1)
                {
                    result = (false, "No puedes desactivar al último administrador activo.");
                    return;
                }

                user.Deactivate();
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
