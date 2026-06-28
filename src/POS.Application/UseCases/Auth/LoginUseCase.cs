using POS.Application.DTOs.Auth;
using POS.Application.Interfaces;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed class LoginUseCase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHashService _hasher;

    public LoginUseCase(IUserRepository users, IPasswordHashService hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    public async Task<LoginResult> ExecuteAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return new LoginResult(false, "Usuario y contraseña son requeridos.");

        var user = await _users.GetByUsernameAsync(request.Username.Trim(), ct);
        if (user is null || !user.IsActive)
            return new LoginResult(false, "Usuario no encontrado o inactivo.");

        if (!_hasher.VerifyPassword(request.Password, user.PasswordHash))
            return new LoginResult(false, "Contraseña incorrecta.");

        return new LoginResult(true, null, user.Id, user.Username, user.Role);
    }
}
