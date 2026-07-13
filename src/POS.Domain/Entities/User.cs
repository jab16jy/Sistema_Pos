namespace POS.Domain.Entities;

public sealed class User
{
    private User()
    {
        Username = string.Empty;
        PasswordHash = string.Empty;
    }

    public User(string username, string passwordHash, Guid roleId)
    {
        Id = Guid.NewGuid();
        Username = string.IsNullOrWhiteSpace(username) ? throw new ArgumentException("Username is required.", nameof(username)) : username.Trim();
        PasswordHash = string.IsNullOrWhiteSpace(passwordHash) ? throw new ArgumentException("Password hash is required.", nameof(passwordHash)) : passwordHash;
        RoleId = roleId == Guid.Empty ? throw new ArgumentException("Role id is required.", nameof(roleId)) : roleId;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Username { get; private set; }
    public string PasswordHash { get; private set; }
    public Guid RoleId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void UpdatePasswordHash(string newHash)
    {
        if (string.IsNullOrWhiteSpace(newHash)) throw new ArgumentException("Hash is required.", nameof(newHash));
        PasswordHash = newHash;
    }

    public void ChangeRole(Guid roleId) => RoleId = roleId == Guid.Empty ? throw new ArgumentException("Role id is required.", nameof(roleId)) : roleId;

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
