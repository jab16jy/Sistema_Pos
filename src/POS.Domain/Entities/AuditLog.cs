namespace POS.Domain.Entities;

public sealed class AuditLog
{
    private AuditLog()
    {
        Username = string.Empty;
        Action = string.Empty;
        EntityName = string.Empty;
    }

    public AuditLog(Guid? userId, string? username, string action, string entityName, string? entityId, string? details = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Username = string.IsNullOrWhiteSpace(username) ? "Sistema" : username.Trim();
        Action = string.IsNullOrWhiteSpace(action) ? throw new ArgumentException("Action is required.", nameof(action)) : action.Trim();
        EntityName = string.IsNullOrWhiteSpace(entityName) ? throw new ArgumentException("Entity name is required.", nameof(entityName)) : entityName.Trim();
        EntityId = string.IsNullOrWhiteSpace(entityId) ? null : entityId.Trim();
        Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public string Username { get; private set; }
    public string Action { get; private set; }
    public string EntityName { get; private set; }
    public string? EntityId { get; private set; }
    public string? Details { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
