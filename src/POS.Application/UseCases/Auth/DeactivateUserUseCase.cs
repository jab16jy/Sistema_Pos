using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed class DeactivateUserUseCase
{
    private readonly IUserRepository _users;

    public DeactivateUserUseCase(IUserRepository users) => _users = users;

    public async Task<(bool Success, string? Error)> ExecuteAsync(
        Guid userId, Guid requestingUserId, CancellationToken ct = default)
    {
        if (userId == requestingUserId)
            return (false, "No puedes desactivar tu propio usuario.");

        var authorizationError = await AdminUserAuthorization.EnsureAdministratorAsync(
            _users,
            requestingUserId,
            "Solo un administrador puede desactivar usuarios.",
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
