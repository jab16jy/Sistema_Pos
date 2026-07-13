namespace POS.Application.Interfaces;

public interface ICurrentOperatorSession
{
    Guid OperatorId { get; }
    string Username { get; }
    Guid RoleId => Guid.Empty;
    string RoleName => string.Empty;
    IReadOnlySet<string> Permissions => new HashSet<string>();
    bool IsAuthenticated { get; }
    bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
}
