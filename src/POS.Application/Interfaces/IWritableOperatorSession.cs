namespace POS.Application.Interfaces;

public interface IWritableOperatorSession : ICurrentOperatorSession
{
    void SetUser(Guid id, string username, Guid roleId, string roleName, IReadOnlySet<string> permissions);
    void ClearSession();
}
