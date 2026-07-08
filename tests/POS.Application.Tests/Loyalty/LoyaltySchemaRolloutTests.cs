using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

                var product = new Product("Tratamiento", Guid.NewGuid(), new Money(300_000m), new Money(700_000m), new StockQuantity(5m), new StockQuantity(1m));
                db.Products.Add(product);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);

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
}
