namespace POS.Application.DTOs.Audit;

public sealed record AuditLogEntry(
    Guid Id,
    Guid? UserId,
    string Username,
    string Action,
    string EntityName,
    string? EntityId,
    string? Details,
    DateTimeOffset CreatedAt);
