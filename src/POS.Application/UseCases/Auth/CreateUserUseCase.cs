using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed class CreateUserUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHashService _hasher;

    public CreateUserUseCase(IUserRepository users, IPasswordHashService hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    public async Task<(bool Success, string? Error)> ExecuteAsync(
        string username, string password, UserRole role, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            return (false, "El nombre de usuario es requerido.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            return (false, "La contraseña debe tener al menos 4 caracteres.");

        var existing = await _users.GetByUsernameAsync(username.Trim(), ct);
        if (existing is not null)
            return (false, "Ya existe un usuario con ese nombre.");

        var hash = _hasher.HashPassword(password);
        var user = new User(username.Trim(), hash, role);
        await _users.AddAsync(user, ct);
        return (true, null);
    }
}
