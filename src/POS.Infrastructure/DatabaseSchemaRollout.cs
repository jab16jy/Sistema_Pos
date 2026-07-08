using Microsoft.EntityFrameworkCore;
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

        foreach (Sale sale in salesWithCustomer)
        {
            bool alreadyEarned = await dbContext.LoyaltyTransactions
                .AsNoTracking()
                .AnyAsync(transaction => transaction.SaleId == sale.Id && transaction.Type == LoyaltyTransactionType.Earned, ct)
                .ConfigureAwait(false);

            if (alreadyEarned || sale.CustomerId is null)
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
