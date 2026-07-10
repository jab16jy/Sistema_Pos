using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Tests;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests.Loyalty;

public sealed class LoyaltySchemaRolloutTests
{
    [Fact]
    public async Task EnsureLatestSchemaAsync_SeedsDefaultRuleAndBackfillsCompletedCustomerSales()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"pos-loyalty-{Guid.NewGuid():N}.db");

        try
        {
            var services = new ServiceCollection();
            services.AddPosInfrastructure($"Data Source={dbPath}");
            await using ServiceProvider provider = services.BuildServiceProvider();

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            using (IServiceScope setupScope = provider.CreateScope())
            {
                var db = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.LoyaltyRules.RemoveRange(db.LoyaltyRules);

                var customer = new Customer("Ana Gomez", "123");
                db.Customers.Add(customer);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);

                Product product = await TestProductFactory.CreateAndPersistAsync(db, "Tratamiento", new Money(300_000m), new Money(700_000m), new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);

                var sale = new Sale(Guid.NewGuid(), customer.Id);
                sale.AddItem(product, new Quantity(1m), Money.Zero);
                sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(700_000m), new Money(700_000m), null));
                sale.Complete();

                db.Sales.Add(sale);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            using IServiceScope assertScope = provider.CreateScope();
            var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();

            LoyaltyRule rule = await assertDb.LoyaltyRules.SingleAsync(rule => rule.IsActive, TestContext.Current.CancellationToken);
            LoyaltyTransaction transaction = await assertDb.LoyaltyTransactions.SingleAsync(TestContext.Current.CancellationToken);

            Assert.Equal(0.001m, rule.PointsPerCurrencyUnit);
            Assert.Equal(LoyaltyTransactionType.Earned, transaction.Type);
            Assert.Equal(700, transaction.Points);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task EnsureLatestSchemaAsync_NormalizesLegacyUnixMillisecondsSaleCreatedAtBeforeBackfill()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"pos-loyalty-{Guid.NewGuid():N}.db");

        try
        {
            var services = new ServiceCollection();
            services.AddPosInfrastructure($"Data Source={dbPath}");
            await using ServiceProvider provider = services.BuildServiceProvider();

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            Guid saleId;
            using (IServiceScope setupScope = provider.CreateScope())
            {
                var db = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();

                var customer = new Customer("Ana Gomez", "123");
                db.Customers.Add(customer);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);

                Product product = await TestProductFactory.CreateAndPersistAsync(db, "Tratamiento", new Money(300_000m), new Money(700_000m), new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);

                var sale = new Sale(Guid.NewGuid(), customer.Id);
                sale.AddItem(product, new Quantity(1m), Money.Zero);
                sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(700_000m), new Money(700_000m), null));
                sale.Complete();

                db.Sales.Add(sale);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                saleId = sale.Id;
            }

            await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "UPDATE SALES SET CreatedAt = '1783564088164' WHERE Id = $id";
                command.Parameters.AddWithValue("$id", saleId.ToString());
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            using IServiceScope assertScope = provider.CreateScope();
            var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();

            LoyaltyTransaction transaction = await assertDb.LoyaltyTransactions.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(700, transaction.Points);

            await using (var connection = new SqliteConnection($"Data Source={dbPath}"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "SELECT CreatedAt FROM SALES WHERE Id = $id";
                command.Parameters.AddWithValue("$id", saleId.ToString());

                string normalizedCreatedAt = Assert.IsType<string>(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
                Assert.NotEqual("1783564088164", normalizedCreatedAt);
                Assert.EndsWith("+00:00", normalizedCreatedAt);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task EnsureLatestSchemaAsync_SkipsSaleWithOrphanedCustomerId()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"pos-loyalty-{Guid.NewGuid():N}.db");

        try
        {
            var services = new ServiceCollection();
            services.AddPosInfrastructure($"Data Source={dbPath}");
            await using ServiceProvider provider = services.BuildServiceProvider();

            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            Guid orphanedCustomerId = Guid.NewGuid();

            using (IServiceScope setupScope = provider.CreateScope())
            {
                var db = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();

                Product product = await TestProductFactory.CreateAndPersistAsync(db, "Tratamiento", new Money(300_000m), new Money(700_000m), new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);

                // Simulates historical data where the referenced Customer row no longer exists.
                var sale = new Sale(Guid.NewGuid(), orphanedCustomerId);
                sale.AddItem(product, new Quantity(1m), Money.Zero);
                sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(700_000m), new Money(700_000m), null));
                sale.Complete();

                db.Sales.Add(sale);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Must not throw DbUpdateException (FK constraint) and must not crash startup.
            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            using IServiceScope assertScope = provider.CreateScope();
            var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();

            Assert.False(await assertDb.LoyaltyAccounts.AnyAsync(account => account.CustomerId == orphanedCustomerId, TestContext.Current.CancellationToken));
            Assert.False(await assertDb.LoyaltyTransactions.AnyAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}
