using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests.Loyalty;

public sealed class LoyaltyRepositoryTests
{
    [Fact]
    public async Task GetOrCreateAccountAsync_IsIdempotentForTheSameCustomer()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var customer = new Customer("Ana Gomez", "123");
        db.Customers.Add(customer);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new LoyaltyRepository(db);
        LoyaltyAccount first = await repository.GetOrCreateAccountAsync(customer.Id, TestContext.Current.CancellationToken);
        LoyaltyAccount second = await repository.GetOrCreateAccountAsync(customer.Id, TestContext.Current.CancellationToken);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.LoyaltyAccounts.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateRuleAsync_UpdatesDetachedRuleWhenSameRuleIsAlreadyTracked()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var persistedRule = new LoyaltyRule(0.001m, 10m, 100, "Inicial");
        db.LoyaltyRules.Add(persistedRule);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        LoyaltyRule tracked = await db.LoyaltyRules.SingleAsync(TestContext.Current.CancellationToken);
        LoyaltyRule detached = await db.LoyaltyRules.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        detached.Deactivate();

        var repository = new LoyaltyRepository(db);
        await repository.UpdateRuleAsync(detached, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(tracked.IsActive);
        Assert.False((await db.LoyaltyRules.SingleAsync(TestContext.Current.CancellationToken)).IsActive);
    }

    [Fact]
    public async Task LoyaltyLedger_UsesTypedMetrics_SignedBalance_AndUniqueCompensationLinks()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var tier = new LoyaltyTier("Bronze", 0m);
        var customer = new Customer("Ana Gomez", "123");
        var account = new LoyaltyAccount(customer.Id);
        var saleId = Guid.NewGuid();
        var earned = new LoyaltyTransaction(account.Id, LoyaltyTransactionType.Earned, 100, saleId);
        var redeemed = new LoyaltyTransaction(account.Id, LoyaltyTransactionType.Redeemed, -40, saleId);
        db.AddRange(tier, customer, account, earned, redeemed);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.AddRange(LoyaltyTransaction.CreateCompensation(earned, "Return"), LoyaltyTransaction.CreateCompensation(redeemed, "Return"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new LoyaltyRepository(db);
        Assert.Equal(LoyaltyTierTextForeground.White, (await repository.GetTiersAsync(TestContext.Current.CancellationToken)).Single().TextForeground);
        Assert.Equal(0, await repository.GetBalanceAsync(account.Id, TestContext.Current.CancellationToken));
        Assert.Equal(100, await repository.GetTotalIssuedPointsAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await repository.GetRedemptionCountAsync(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1), TestContext.Current.CancellationToken));

        db.LoyaltyTransactions.Add(LoyaltyTransaction.CreateCompensation(earned, "Duplicate"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
