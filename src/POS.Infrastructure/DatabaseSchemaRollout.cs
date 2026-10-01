using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Catalog;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Preflight;

namespace POS.Infrastructure;

public static class DatabaseSchemaRollout
{
    public static async Task EnsureLatestSchemaAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SnapshotBeforePendingMigrationsAsync(scope.ServiceProvider, dbContext, cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
        // Runs after MigrateAsync, not before: the orphan cleanup that makes a legacy database pass
        // this check lives inside the migrations themselves, so gating on it first meant a dirty
        // database could never reach the migration that repairs it. This is the post-migration
        // verification instead — nothing that survives the cleanup may violate a relationship.
        await IntegrityPreflight.VerifyAsync(dbContext, cancellationToken);
        await NormalizeLegacyUnixTimestampDateTimesAsync(dbContext, cancellationToken);
        await SeedDefaultRolesAndAdminAsync(scope.ServiceProvider, dbContext, cancellationToken);
        await SeedDefaultLoyaltyProgramAsync(dbContext, cancellationToken);
        await BackfillMissingLoyaltyPointsAsync(dbContext, cancellationToken);
        await CatalogWorkbookBootstrapper.SeedAsync(dbContext, cancellationToken);
    }

    /// <summary>
    /// Snapshots the database before an upgrade applies pending migrations.
    /// </summary>
    /// <remarks>
    /// The only backup tool this app has lives behind the login screen, so once an upgrade fails at
    /// startup the user can no longer take one — the moment a snapshot is still possible is right
    /// here, before the first migration touches anything. Only runs when migrations are actually
    /// pending, so a normal launch on an up-to-date database costs nothing. A failure to snapshot
    /// aborts startup on purpose: migrating irreplaceable data with no way back is the worse outcome.
    /// </remarks>
    private static async Task SnapshotBeforePendingMigrationsAsync(IServiceProvider sp, AppDbContext dbContext, CancellationToken ct)
    {
        // A database with no applied migrations is a brand-new install with nothing to lose, and
        // opening the connection above already created its (empty) file — snapshotting it would
        // leave a useless backup next to every fresh installation.
        IEnumerable<string> applied = await dbContext.Database.GetAppliedMigrationsAsync(ct).ConfigureAwait(false);
        if (!applied.Any())
        {
            return;
        }

        IEnumerable<string> pending = await dbContext.Database.GetPendingMigrationsAsync(ct).ConfigureAwait(false);
        if (!pending.Any())
        {
            return;
        }

        var backupService = sp.GetService<IManualBackupService>();
        string? databasePath = dbContext.Database.GetDbConnection().DataSource;
        if (backupService is null || string.IsNullOrWhiteSpace(databasePath) || !File.Exists(databasePath))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        await backupService.CreateBackupAsync(directory, ct).ConfigureAwait(false);
    }

    private static async Task SeedDefaultRolesAndAdminAsync(IServiceProvider sp, AppDbContext dbContext, CancellationToken ct)
    {
        if (!await dbContext.Roles.AnyAsync(ct))
        {
            dbContext.Roles.AddRange(
                Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName),
                Role.CreateSystemRole(RoleConstants.CashierRoleId, RoleConstants.CashierRoleName));
            await dbContext.SaveChangesAsync(ct);
        }

        // Grants the cashier set only when the role has zero rows — a fresh install. This is
        // deliberately NOT a "converge on CashierDefaults" backfill: RolePermission has no tombstone
        // distinguishing "never granted" from "revoked by an administrator", and UpdateRoleUseCase
        // only shields the Administrator role, so a per-launch converging insert would silently
        // restore any cashier key an administrator removed, forever. Delivering a new key to an
        // existing role is a data migration's job (see Migrations/…_GrantQuotesEditToCashierRole),
        // which runs once inside the pre-migration snapshot envelope.
        var cashierRoleHasNoPermissions = !await dbContext.RolePermissions
            .AnyAsync(permission => permission.RoleId == RoleConstants.CashierRoleId, ct);
        if (cashierRoleHasNoPermissions)
        {
            dbContext.RolePermissions.AddRange(PermissionKeys.CashierDefaults.Select(key => new RolePermission(RoleConstants.CashierRoleId, key)));
            await dbContext.SaveChangesAsync(ct);
        }

        if (await dbContext.Users.AnyAsync(ct))
            return;

        var hasher = sp.GetRequiredService<IPasswordHashService>();
        var admin = new User("admin", hasher.HashPassword("admin123"), RoleConstants.AdministratorRoleId);
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(ct);
    }

    private static async Task SeedDefaultLoyaltyProgramAsync(AppDbContext dbContext, CancellationToken ct)
    {
        // Redemption is granted by a tier, so a program with no tiers at all can never redeem —
        // including installs that predate tiers and already have a rule. Seeding a base tier keeps
        // the seeded redemption rule usable and gives the business something to edit in the module.
        bool hasAnyTier = await dbContext.LoyaltyTiers.AnyAsync(ct).ConfigureAwait(false);
        if (!hasAnyTier)
        {
            dbContext.LoyaltyTiers.Add(new LoyaltyTier(
                "Base",
                minPoints: 0m,
                benefits: "Acumulación de puntos en cada compra",
                allowsRedemption: true));
            await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        bool hasActiveRule = await dbContext.LoyaltyRules.AnyAsync(rule => rule.IsActive, ct).ConfigureAwait(false);
        if (hasActiveRule)
        {
            return;
        }

        var defaultRule = new LoyaltyRule(
            pointsPerCurrencyUnit: 0.001m,
            redemptionValuePerPoint: 10m,
            minimumRedeemPoints: 100,
            description: "Regla inicial: 1 punto por cada $1.000 en compras; cada punto descuenta $10.");

        dbContext.LoyaltyRules.Add(defaultRule);
        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task NormalizeLegacyUnixTimestampDateTimesAsync(AppDbContext dbContext, CancellationToken ct)
    {
        var normalizedColumns = new HashSet<(string? Schema, string Table, string Column)>();

        foreach (var entityType in dbContext.Model.GetEntityTypes())
        {
            string? tableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName))
            {
                continue;
            }

            string? schema = entityType.GetSchema();
            var storeObject = StoreObjectIdentifier.Table(tableName, schema);

            foreach (var property in entityType.GetProperties())
            {
                Type propertyType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (propertyType != typeof(DateTimeOffset))
                {
                    continue;
                }

                string? columnName = property.GetColumnName(storeObject);
                if (string.IsNullOrWhiteSpace(columnName) || !normalizedColumns.Add((schema, tableName, columnName)))
                {
                    continue;
                }

                string table = QualifySqliteIdentifier(schema, tableName);
                string column = QuoteSqliteIdentifier(columnName);

                // Some legacy rows were written as JavaScript/Unix milliseconds (e.g. 1783564088164)
                // into TEXT DateTimeOffset columns. EF materialization parses those columns before
                // startup backfills run, so normalize them in raw SQL first. The update is idempotent:
                // only 13-digit numeric values are converted to the fixed UTC TEXT format used by the model.
                string sql = $"""
                    UPDATE {table}
                    SET {column} = strftime('%Y-%m-%d %H:%M:%f', CAST(TRIM(CAST({column} AS TEXT)) AS INTEGER) / 1000.0, 'unixepoch') || '0000+00:00'
                    WHERE {column} IS NOT NULL
                      AND length(TRIM(CAST({column} AS TEXT))) = 13
                      AND TRIM(CAST({column} AS TEXT)) NOT GLOB '*[^0-9]*'
                    """;

                await dbContext.Database.ExecuteSqlRawAsync(sql, ct).ConfigureAwait(false);
            }
        }
    }

    private static string QualifySqliteIdentifier(string? schema, string tableName) =>
        string.IsNullOrWhiteSpace(schema)
            ? QuoteSqliteIdentifier(tableName)
            : $"{QuoteSqliteIdentifier(schema)}.{QuoteSqliteIdentifier(tableName)}";

    private static string QuoteSqliteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    private static async Task BackfillMissingLoyaltyPointsAsync(AppDbContext dbContext, CancellationToken ct)
    {
        LoyaltyRule? activeRule = await dbContext.LoyaltyRules
            .AsNoTracking()
            .FirstOrDefaultAsync(rule => rule.IsActive, ct)
            .ConfigureAwait(false);

        if (activeRule is null)
        {
            return;
        }

        List<Sale> salesWithCustomer = await dbContext.Sales
            .Include(sale => sale.Items)
            .Where(sale => sale.Status == SaleStatus.Completed && sale.CustomerId != null)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // ponytail: a Sale can reference a CustomerId whose Customer row no longer exists
        // (orphaned historical data). Creating a LoyaltyAccount for it would violate the FK
        // and abort startup entirely, so those sales are skipped instead of crashing the app.
        HashSet<Guid> existingCustomerIds = (await dbContext.Customers
            .AsNoTracking()
            .Select(customer => customer.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false)).ToHashSet();

        foreach (Sale sale in salesWithCustomer)
        {
            if (sale.CustomerId is null || !existingCustomerIds.Contains(sale.CustomerId.Value))
            {
                continue;
            }

            bool alreadyEarned = await dbContext.LoyaltyTransactions
                .AsNoTracking()
                .AnyAsync(transaction => transaction.SaleId == sale.Id && transaction.Type == LoyaltyTransactionType.Earned, ct)
                .ConfigureAwait(false);

            if (alreadyEarned)
            {
                continue;
            }

            int points = activeRule.PointsFor(new Money(sale.Total().Amount));
            if (points == 0)
            {
                continue;
            }

            LoyaltyAccount account = await GetOrCreateLoyaltyAccountAsync(dbContext, sale.CustomerId.Value, ct)
                .ConfigureAwait(false);

            dbContext.LoyaltyTransactions.Add(new LoyaltyTransaction(
                account.Id,
                LoyaltyTransactionType.Earned,
                points,
                sale.Id,
                $"Puntos acumulados por venta {sale.InvoiceNumber}"));
        }

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task<LoyaltyAccount> GetOrCreateLoyaltyAccountAsync(
        AppDbContext dbContext,
        Guid customerId,
        CancellationToken ct)
    {
        LoyaltyAccount? existing = await dbContext.LoyaltyAccounts
            .FirstOrDefaultAsync(account => account.CustomerId == customerId, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        // SQLite enforces the one-account-per-customer invariant at the database boundary.
        // INSERT OR IGNORE closes the check-then-insert race between startup and another writer.
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT OR IGNORE INTO "LOYALTY_ACCOUNTS" ("Id", "CustomerId", "IsActive", "CreatedAt")
            VALUES ({Guid.NewGuid().ToString()}, {customerId.ToString()}, {true}, {DateTimeOffset.UtcNow});
            """, ct).ConfigureAwait(false);

        return await dbContext.LoyaltyAccounts
            .FirstOrDefaultAsync(account => account.CustomerId == customerId, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The loyalty account could not be created.");
    }
}
