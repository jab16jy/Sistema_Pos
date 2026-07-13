using POS.Domain.Authorization;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed class DeactivateUserUseCase
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;

    public DeactivateUserUseCase(IUserRepository users, IRoleRepository roles) => (_users, _roles) = (users, roles);
    public async Task<(bool Success, string? Error)> ExecuteAsync(
        Guid userId, Guid requestingUserId, CancellationToken ct = default)
    {
        if (userId == requestingUserId)
            return (false, "No puedes desactivar tu propio usuario.");

        var authorizationError = await PermissionAuthorization.EnsureHasPermissionAsync(
            _users,
            _roles,
            requestingUserId,
            PermissionKeys.Users.Deactivate,
            "No tienes permiso para desactivar usuarios.",
            ct).ConfigureAwait(false);
        if (authorizationError is not null)
            return (false, authorizationError);

        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null) return (false, "Usuario no encontrado.");

        user.Deactivate();
        await _users.UpdateAsync(user, ct);
        return (true, null);
    }
}
