using POS.Application.Interfaces;
namespace POS.Infrastructure.Persistence;

public sealed class InMemoryUserSession : IWritableOperatorSession
{
    private Guid _operatorId = Guid.Empty;
    private string _username = string.Empty;
    private Guid _roleId = Guid.Empty;
    private string _roleName = string.Empty;
    private IReadOnlySet<string> _permissions = new HashSet<string>();

    public Guid OperatorId => _operatorId;
    public string Username => _username;
    public Guid RoleId => _roleId;
    public string RoleName => _roleName;
    public IReadOnlySet<string> Permissions => _permissions;
    public bool IsAuthenticated => _operatorId != Guid.Empty;
    public bool HasPermission(string permissionKey) => _permissions.Contains(permissionKey);

    public void SetUser(Guid id, string username, Guid roleId, string roleName, IReadOnlySet<string> permissions)
    {
        _operatorId = id;
        _username = username;
        _roleId = roleId;
        _roleName = roleName;
        _permissions = new HashSet<string>(permissions ?? new HashSet<string>(), StringComparer.Ordinal);
    }

    public void ClearSession()
    {
        _operatorId = Guid.Empty;
        _username = string.Empty;
        _roleId = Guid.Empty;
        _roleName = string.Empty;
        _permissions = new HashSet<string>();
    }
}
