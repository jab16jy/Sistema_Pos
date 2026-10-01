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
/// I6's second half: the cashier role must actually be able to edit quotes.
/// </summary>
/// <remarks>
/// This permission was previously delivered by a per-launch "insert whatever is missing" backfill in
/// <c>DatabaseSchemaRollout</c>. That was a silent privilege-escalation loop rather than a fix:
/// <c>RolePermission</c> has no tombstone separating "never granted" from "revoked by an
/// administrator", and <c>UpdateRoleUseCase</c> only shields the Administrator role, so a cashier key
/// an administrator removed came straight back on the next launch, forever. It also mutated the live
/// database outside the pre-migration snapshot envelope, which is how a transient "database is
/// locked" could escalate into a boot loop. The delivery is now a one-time data migration inside
/// <see cref="Migrations.GrantQuotesEditToCashierRole"/>, and these tests pin both halves of the
/// contract: existing installs get the key from the migration, fresh installs get it from the seed,
/// and a deliberately removed key stays removed.
/// </remarks>
public sealed class CashierPermissionBackfillTests
{
    private const string MigrationGrantingQuotesEdit = "20261001183418_GrantQuotesEditToCashierRole";

    [Fact]
    public async Task Migration_OnAnInstallThatPredatesTheNewKey_GrantsItToTheCashierRoleOnce()
    {
        // Reproduce the real shape of an affected install: the schema and the Cashier role exist and
        // already carry permissions, but Quotes.Edit predates them. The migration — not the rollout
        // seed — is what has to close the gap.
        string databasePath = Path.Combine(Path.GetTempPath(), $"pos-cashier-migration-{Guid.NewGuid():N}.db");
        try
        {
            await using var provider = new ServiceCollection()
                .AddPosInfrastructure($"Data Source={databasePath}")
                .BuildServiceProvider();

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            // Rewind to the pre-migration state: key removed, and the migration itself unapplied so
            // MigrateAsync has real work to do.
            await RewindToPreMigrationStateAsync(databasePath);

            await MigrateAsync(databasePath);

            await using var dbContext = CreateDbContext(databasePath);
            var roles = new RoleRepository(dbContext);
            var granted = await roles.GetPermissionKeysAsync(RoleConstants.CashierRoleId, TestContext.Current.CancellationToken);

            Assert.Contains(PermissionKeys.Quotes.Edit, granted);

            // One-time, not idempotent-on-every-launch: re-running the migration must not pile up
            // duplicate rows.
            Assert.Equal(
                granted.Distinct(StringComparer.Ordinal).Count(),
                await dbContext.RolePermissions.CountAsync(
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
    public async Task EnsureLatestSchemaAsync_OnAFreshInstall_GrantsTheWholeCashierDefaultSet()
    {
        // A fresh install never sees the migration's conditional insert do anything — the Cashier role
        // does not exist yet when migrations run, because SeedDefaultRolesAndAdminAsync runs after
        // MigrateAsync. The seed is what covers a role with zero rows.
        string databasePath = Path.Combine(Path.GetTempPath(), $"pos-cashier-fresh-{Guid.NewGuid():N}.db");
        try
        {
            await using var provider = new ServiceCollection()
                .AddPosInfrastructure($"Data Source={databasePath}")
                .BuildServiceProvider();

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            await using var dbContext = CreateDbContext(databasePath);
            var roles = new RoleRepository(dbContext);
            var granted = await roles.GetPermissionKeysAsync(RoleConstants.CashierRoleId, TestContext.Current.CancellationToken);

            Assert.True(PermissionKeys.CashierDefaults.SetEquals(granted));
            Assert.True(await roles.HasPermissionAsync(RoleConstants.CashierRoleId, PermissionKeys.Quotes.Edit, TestContext.Current.CancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task EnsureLatestSchemaAsync_WhenAnAdministratorRemovedAKey_DoesNotReAddItOnALaterLaunch()
    {
        // The regression this replaces: a converging per-launch insert makes an administrator's
        // revocation of Invoices.Return silently unrecoverable. Three launches, same answer.
        string databasePath = Path.Combine(Path.GetTempPath(), $"pos-cashier-revoked-{Guid.NewGuid():N}.db");
        try
        {
            await using var provider = new ServiceCollection()
                .AddPosInfrastructure($"Data Source={databasePath}")
                .BuildServiceProvider();

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            await using (var revoke = CreateDbContext(databasePath))
            {
                await revoke.RolePermissions
                    .Where(permission => permission.RoleId == RoleConstants.CashierRoleId
                                         && permission.PermissionKey == PermissionKeys.Invoices.Return)
                    .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            }

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);
            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);
            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            await using var dbContext = CreateDbContext(databasePath);
            var roles = new RoleRepository(dbContext);

            Assert.False(await roles.HasPermissionAsync(RoleConstants.CashierRoleId, PermissionKeys.Invoices.Return, TestContext.Current.CancellationToken));
            // Removing one key must not empty the role either: everything else survives the launches.
            Assert.Contains(PermissionKeys.Quotes.Edit, await roles.GetPermissionKeysAsync(RoleConstants.CashierRoleId, TestContext.Current.CancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    /// <summary>
    /// Puts the database back to the moment before the grant migration: key removed and the migration
    /// row removed from __EFMigrationsHistory, so MigrateAsync re-runs it.
    /// </summary>
    private static async Task RewindToPreMigrationStateAsync(string databasePath)
    {
        await using var dbContext = CreateDbContext(databasePath);
        await dbContext.RolePermissions
            .Where(permission => permission.RoleId == RoleConstants.CashierRoleId
                                 && permission.PermissionKey == PermissionKeys.Quotes.Edit)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            $"DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{MigrationGrantingQuotesEdit}';",
            TestContext.Current.CancellationToken);
    }

    private static async Task MigrateAsync(string databasePath)
    {
        await using var dbContext = CreateDbContext(databasePath);
        await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    private static AppDbContext CreateDbContext(string databasePath) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={databasePath}").Options);
}
