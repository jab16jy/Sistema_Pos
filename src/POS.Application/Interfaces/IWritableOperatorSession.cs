using POS.Domain.Enums;

namespace POS.Application.Interfaces;

public interface IWritableOperatorSession : ICurrentOperatorSession
{
    void SetUser(Guid id, string username, UserRole role);
    void ClearSession();
}
