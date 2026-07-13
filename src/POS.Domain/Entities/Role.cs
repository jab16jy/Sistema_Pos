namespace POS.Domain.Entities;

public sealed class Role
{
    private Role() => Name = string.Empty;

    public Role(string name)
    {
        Id = Guid.NewGuid();
        Name = NormalizeName(name);
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Role CreateSystemRole(Guid id, string name)
    {
        if (id == Guid.Empty) throw new ArgumentException("Role id is required.", nameof(id));
        var role = new Role
        {
            Id = id,
            Name = NormalizeName(name),
            IsSystem = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        role.UpdatedAt = role.CreatedAt;
        return role;
    }

    public void Rename(string newName) { Name = NormalizeName(newName); Touch(); }
    public void Deactivate() { IsActive = false; Touch(); }
    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
    private static string NormalizeName(string name) => string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Role name is required.", nameof(name)) : name.Trim();
}
