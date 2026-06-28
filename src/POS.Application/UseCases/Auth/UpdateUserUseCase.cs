using POS.Application.Interfaces;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed class UpdateUserUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHashService _hasher;

    public UpdateUserUseCase(IUserRepository users, IPasswordHashService hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    public async Task<(bool Success, string? Error)> ExecuteAsync(
        Guid userId, string? newPassword, UserRole newRole, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId, ct);
        if (user is null) return (false, "Usuario no encontrado.");

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword.Length < 4)
                return (false, "La contraseña debe tener al menos 4 caracteres.");
            user.UpdatePasswordHash(_hasher.HashPassword(newPassword));
        }

        user.UpdateRole(newRole);
        await _users.UpdateAsync(user, ct);
        return (true, null);
    }
}
