using Microsoft.EntityFrameworkCore;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;

namespace POS.Application.Tests;

/// <summary>
/// Persists a minimal cashier User (and its Role) so Sale/SaleReturn FK-enforced writes
/// have a valid CashierUserId/ProcessedByUserId to point at.
/// </summary>
internal static class TestCashierFactory
{
    public static async Task<Guid> CreateAndPersistAsync(AppDbContext dbContext, CancellationToken cancellationToken)
    {
        if (!await dbContext.Roles.AnyAsync(r => r.Id == RoleConstants.AdministratorRoleId, cancellationToken))
        {
            dbContext.Roles.Add(Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName));
        }

        var user = new User($"cashier-{Guid.NewGuid():N}", "hash", RoleConstants.AdministratorRoleId);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
