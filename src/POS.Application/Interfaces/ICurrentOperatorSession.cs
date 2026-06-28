using POS.Domain.Enums;

namespace POS.Application.Interfaces;

public interface ICurrentOperatorSession
{
    Guid OperatorId { get; }
    string Username { get; }
    UserRole Role { get; }
    bool IsAuthenticated { get; }
}
