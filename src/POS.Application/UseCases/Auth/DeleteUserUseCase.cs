using POS.Domain.Authorization;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed class DeleteUserUseCase
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUserUseCase(IUserRepository users, IRoleRepository roles, IUnitOfWork unitOfWork)
        => (_users, _roles, _unitOfWork) = (users, roles, unitOfWork);

    public async Task<(bool Success, string? Error)> ExecuteAsync(
        Guid userId, Guid requestingUserId, CancellationToken ct = default)
    {
        if (userId == requestingUserId)
            return (false, "No puedes eliminar tu propio usuario.");

        (bool Success, string? Error) result = default;
        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
            {
                var authorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
                    _users,
                    _roles,
                    requestingUserId,
                    PermissionKeys.Users.Delete,
                    "No tienes permiso para eliminar usuarios.",
                    transactionCt).ConfigureAwait(false);

                // Fallback to Deactivate permission if Delete is not explicitly configured
                if (authorizationError is not null)
                {
                    authorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
                        _users,
                        _roles,
                        requestingUserId,
                        PermissionKeys.Users.Deactivate,
                        "No tienes permiso para eliminar usuarios.",
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

                if (user.IsActive
                    && RoleConstants.IsSystemAdministrator(user.RoleId)
                    && await _users.CountActiveUsersInRoleAsync(RoleConstants.AdministratorRoleId, transactionCt).ConfigureAwait(false) <= 1)
                {
                    result = (false, "No puedes eliminar al último administrador activo.");
                    return;
                }

                await _users.DeleteAsync(user, transactionCt).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(transactionCt).ConfigureAwait(false);
                result = (true, null);
            }, ct).ConfigureAwait(false);
        }
        catch (TransactionConflictException)
        {
            return (false, "La base de datos está ocupada. Inténtalo de nuevo.");
        }
        catch (Exception ex) when (IsDbUpdateConstraintError(ex))
        {
            return (false, "No se puede eliminar este usuario porque posee historial registrado (ventas, turnos o movimientos). Se recomienda desactivarlo en su lugar.");
        }
        catch (Exception ex)
        {
            return (false, $"Error al eliminar usuario: {ex.Message}");
        }

        return result;
    }

    private static bool IsDbUpdateConstraintError(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var name = current.GetType().Name;
            var message = current.Message;
            if (name.Contains("DbUpdate", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("constraint failed", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("saving the entity changes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
