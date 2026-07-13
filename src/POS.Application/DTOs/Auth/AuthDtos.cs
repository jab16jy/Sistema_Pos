namespace POS.Application.DTOs.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResult(
    bool Success,
    string? ErrorMessage,
    Guid UserId = default,
    string Username = "",
    Guid RoleId = default,
    string RoleName = "",
    IReadOnlySet<string>? Permissions = null);
