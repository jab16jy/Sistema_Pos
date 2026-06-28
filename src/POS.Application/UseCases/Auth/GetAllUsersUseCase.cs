using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed record UserSummary(Guid Id, string Username, UserRole Role, bool IsActive);

public sealed class GetAllUsersUseCase
{
    private readonly IUserRepository _users;

    public GetAllUsersUseCase(IUserRepository users) => _users = users;

    public async Task<IReadOnlyList<UserSummary>> ExecuteAsync(CancellationToken ct = default)
    {
        var users = await _users.GetAllAsync(ct);
        return users.Select(u => new UserSummary(u.Id, u.Username, u.Role, u.IsActive)).ToList();
    }
}
