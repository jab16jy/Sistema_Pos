using POS.Domain.Enums;

namespace POS.Application.DTOs.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResult(bool Success, string? ErrorMessage, Guid UserId = default, string Username = "", UserRole Role = UserRole.Cashier);
