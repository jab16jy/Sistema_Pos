using POS.Application.Interfaces;
using POS.Domain.Authorization;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

public sealed record UserSummary(Guid Id, string Username, Guid RoleId, bool IsActive);

public sealed class GetAllUsersUseCase
{
    private readonly IUserRepository _users;
    private readonly ICurrentOperatorSession _session;

    public GetAllUsersUseCase(IUserRepository users, ICurrentOperatorSession session) => (_users, _session) = (users, session);

    public async Task<IReadOnlyList<UserSummary>> ExecuteAsync(CancellationToken ct = default)
    {
        if (PermissionAuthorization.EnsureHasPermission(_session, PermissionKeys.Users.View, "Unauthorized") is not null)
        {
            throw new UnauthorizedActionException("GetAllUsers");
        }

        var users = await _users.GetAllAsync(ct);
        return users.Select(u => new UserSummary(u.Id, u.Username, u.RoleId, u.IsActive)).ToList();
    }
}
