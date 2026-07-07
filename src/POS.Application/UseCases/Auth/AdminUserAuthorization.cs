using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Auth;

internal static class AdminUserAuthorization
{
    public static async Task<string?> EnsureAdministratorAsync(
        IUserRepository users,
        Guid requestingUserId,
        string errorMessage,
        CancellationToken ct)
    {
        if (requestingUserId == Guid.Empty)
        {
            return errorMessage;
        }

        var requester = await users.GetByIdAsync(requestingUserId, ct).ConfigureAwait(false);
        return requester is { IsActive: true, Role: UserRole.Administrator } ? null : errorMessage;
    }
}
