using POS.Application.Interfaces;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence;

public sealed class InMemoryUserSession : IWritableOperatorSession
{
    private Guid _operatorId = Guid.Empty;
    private string _username = string.Empty;
    private UserRole _role = UserRole.Cashier;

    public Guid OperatorId => _operatorId;
    public string Username => _username;
    public UserRole Role => _role;
    public bool IsAuthenticated => _operatorId != Guid.Empty;

    public void SetUser(Guid id, string username, UserRole role)
    {
        _operatorId = id;
        _username = username;
        _role = role;
    }
}
