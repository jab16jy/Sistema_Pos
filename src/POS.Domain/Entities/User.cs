using POS.Domain.Enums;

namespace POS.Domain.Entities;

public sealed class User
{
    private User()
    {
        Username = string.Empty;
        PasswordHash = string.Empty;
    }

    public User(string username, string passwordHash, UserRole role)
    {
        Id = Guid.NewGuid();
        Username = string.IsNullOrWhiteSpace(username) ? throw new ArgumentException("Username is required.", nameof(username)) : username.Trim();
        PasswordHash = string.IsNullOrWhiteSpace(passwordHash) ? throw new ArgumentException("Password hash is required.", nameof(passwordHash)) : passwordHash;
        Role = role;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string Username { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
