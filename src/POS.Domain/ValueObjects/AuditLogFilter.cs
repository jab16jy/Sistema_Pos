namespace POS.Domain.ValueObjects;

public sealed record AuditLogFilter(
    string? Username = null,
    string? Action = null,
    string? EntityName = null)
{
    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Username) ||
        !string.IsNullOrWhiteSpace(Action) ||
        !string.IsNullOrWhiteSpace(EntityName);
}
