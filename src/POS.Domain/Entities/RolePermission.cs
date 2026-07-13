namespace POS.Domain.Entities;

public sealed class RolePermission
{
    private RolePermission() => PermissionKey = string.Empty;

    public RolePermission(Guid roleId, string permissionKey)
    {
        RoleId = roleId == Guid.Empty ? throw new ArgumentException("Role id is required.", nameof(roleId)) : roleId;
        PermissionKey = string.IsNullOrWhiteSpace(permissionKey) ? throw new ArgumentException("Permission key is required.", nameof(permissionKey)) : permissionKey.Trim();
    }

    public Guid RoleId { get; private set; }
    public string PermissionKey { get; private set; }
}
