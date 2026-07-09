using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Catalog;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure;

public static class DatabaseSchemaRollout
{
    public static async Task EnsureLatestSchemaAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
        await NormalizeLegacyUnixTimestampDateTimesAsync(dbContext, cancellationToken);
        await SeedDefaultAdminAsync(scope.ServiceProvider, dbContext, cancellationToken);
        await SeedDefaultLoyaltyProgramAsync(dbContext, cancellationToken);
        await BackfillMissingLoyaltyPointsAsync(dbContext, cancellationToken);
        await CatalogWorkbookBootstrapper.SeedAsync(dbContext, cancellationToken);
    }

    private static async Task SeedDefaultAdminAsync(IServiceProvider sp, AppDbContext dbContext, CancellationToken ct)
    {
        if (await dbContext.Users.AnyAsync(ct))
            return;

        var hasher = sp.GetRequiredService<IPasswordHashService>();
        var admin = new User("admin", hasher.HashPassword("admin123"), UserRole.Administrator);
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync(ct);
    }

    private static async Task SeedDefaultLoyaltyProgramAsync(AppDbContext dbContext, CancellationToken ct)
    {
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

            LoyaltyAccount? account = await dbContext.LoyaltyAccounts
                .FirstOrDefaultAsync(existing => existing.CustomerId == sale.CustomerId.Value, ct)
                .ConfigureAwait(false);

            if (account is null)
            {
                account = new LoyaltyAccount(sale.CustomerId.Value);
                dbContext.LoyaltyAccounts.Add(account);
                await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            }

            dbContext.LoyaltyTransactions.Add(new LoyaltyTransaction(
                account.Id,
                LoyaltyTransactionType.Earned,
                points,
                sale.Id,
                $"Puntos acumulados por venta {sale.InvoiceNumber}"));
        }

        await dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
