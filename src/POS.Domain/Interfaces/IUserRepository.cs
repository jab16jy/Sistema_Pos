using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

/// <summary>
/// Port for accessing user entities. Used by use cases that need to verify user roles.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
