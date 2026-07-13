using POS.Application.Interfaces;
using POS.Domain.Authorization;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

internal static class PermissionAuthorization
{
    public static string? EnsureHasPermission(
        ICurrentOperatorSession session,
        string permissionKey,
        string errorMessage) => session.IsAuthenticated && session.HasPermission(permissionKey) ? null : errorMessage;

    public static async Task<string?> EnsureHasPermissionAsync(
        IUserRepository users,
        IRoleRepository roles,
        Guid requestingUserId,
        string permissionKey,
        string errorMessage,
        CancellationToken ct)
    {
        if (requestingUserId == Guid.Empty)
            return errorMessage;

        var requester = await users.GetByIdAsync(requestingUserId, ct).ConfigureAwait(false);
        if (requester is not { IsActive: true })
            return errorMessage;

        var role = await roles.GetByIdAsync(requester.RoleId, ct).ConfigureAwait(false);
        if (role is not { IsActive: true })
            return errorMessage;

        if (RoleConstants.IsSystemAdministrator(role.Id))
            return null;

        return await roles.HasPermissionAsync(role.Id, permissionKey, ct).ConfigureAwait(false) ? null : errorMessage;
    }
}
