using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests.Persistence;

/// <summary>
/// I6's second half. Granting <c>PermissionKeys.CashierDefaults</c> only when the cashier role had
/// zero rows meant a key added after an install was born could never reach it — the shop would
/// keep a Cashier role that silently could not do the new thing. The rollout now inserts whatever is
/// missing, which is the fix for every future key too.
/// </summary>
public sealed class CashierPermissionBackfillTests
{
    [Fact]
    public async Task EnsureLatestSchemaAsync_OnAnInstallThatPredatesTheNewKey_GrantsTheMissingKeyWithoutDuplicatingTheRest()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"pos-cashier-backfill-{Guid.NewGuid():N}.db");
        try
        {
            await using var provider = new ServiceCollection()
                .AddPosInfrastructure($"Data Source={databasePath}")
                .BuildServiceProvider();

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            // Rewind the database to the shape an install has when Quotes.Edit does not exist yet.
            await using (var rewind = CreateDbContext(databasePath))
            {
                await rewind.RolePermissions
                    .Where(permission => permission.RoleId == RoleConstants.CashierRoleId
                                         && permission.PermissionKey == PermissionKeys.Quotes.Edit)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);
            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            await using var dbContext = CreateDbContext(databasePath);
            var roles = new RoleRepository(dbContext);
            var granted = await roles.GetPermissionKeysAsync(RoleConstants.CashierRoleId, TestContext.Current.CancellationToken);

            Assert.Contains(PermissionKeys.Quotes.Edit, granted);
            Assert.True(PermissionKeys.CashierDefaults.SetEquals(granted));

            // Idempotent: a third rollout must not pile up duplicate rows.
            Assert.Equal(PermissionKeys.CashierDefaults.Count, await dbContext.RolePermissions.CountAsync(
                permission => permission.RoleId == RoleConstants.CashierRoleId,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task EnsureLatestSchemaAsync_OnAFreshInstall_GrantsQuotesEditToTheCashierRole()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"pos-cashier-fresh-{Guid.NewGuid():N}.db");
        try
        {
            await using var provider = new ServiceCollection()
                .AddPosInfrastructure($"Data Source={databasePath}")
                .BuildServiceProvider();

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            await using var dbContext = CreateDbContext(databasePath);
            var roles = new RoleRepository(dbContext);

            Assert.True(await roles.HasPermissionAsync(RoleConstants.CashierRoleId, PermissionKeys.Quotes.Edit, TestContext.Current.CancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private static AppDbContext CreateDbContext(string databasePath) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={databasePath}").Options);
}