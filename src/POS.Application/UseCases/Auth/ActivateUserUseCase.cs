using POS.Domain.Authorization;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed class ActivateUserUseCase
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateUserUseCase(IUserRepository users, IRoleRepository roles, IUnitOfWork unitOfWork)
        => (_users, _roles, _unitOfWork) = (users, roles, unitOfWork);

    public async Task<(bool Success, string? Error)> ExecuteAsync(
        Guid userId, Guid requestingUserId, CancellationToken ct = default)
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
                    PermissionKeys.Users.Activate,
                    "No tienes permiso para activar usuarios.",
                    transactionCt).ConfigureAwait(false);

                // Fallback to Deactivate permission if Activate isn't assigned explicitly
                if (authorizationError is not null)
                {
                    authorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
                        _users,
                        _roles,
                        requestingUserId,
                        PermissionKeys.Users.Deactivate,
                        "No tienes permiso para activar usuarios.",
                        transactionCt).ConfigureAwait(false);
                }

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

                user.Activate();
                await _users.UpdateAsync(user, transactionCt).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(transactionCt).ConfigureAwait(false);
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
