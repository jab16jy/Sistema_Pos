using POS.Application.Interfaces;
using POS.Domain.Authorization;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;

namespace POS.Application.Services;

public sealed class CriticalCommandAuthorizer
{
    private readonly ICurrentOperatorSession _session;
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;

    public CriticalCommandAuthorizer(ICurrentOperatorSession session, IUserRepository users, IRoleRepository roles)
    {
        _session = session;
        _users = users;
        _roles = roles;
    }

    public async Task<CanonicalActor> AuthorizeAsync(string permissionKey, CancellationToken ct = default)
    {
        if (!_session.IsAuthenticated || _session.OperatorId == Guid.Empty)
            throw new UnauthorizedActionException(permissionKey);

        var user = await _users.GetByIdAsync(_session.OperatorId, ct).ConfigureAwait(false);
        if (user is not { IsActive: true })
            throw new UnauthorizedActionException(permissionKey);

        var role = await _roles.GetByIdAsync(user.RoleId, ct).ConfigureAwait(false);
        if (role is not { IsActive: true })
            throw new UnauthorizedActionException(permissionKey);

        if (!RoleConstants.IsSystemAdministrator(role.Id)
            && !await _roles.HasPermissionAsync(role.Id, permissionKey, ct).ConfigureAwait(false))
            throw new UnauthorizedActionException(permissionKey);

        return new CanonicalActor(user.Id, user.Username);
    }
}
