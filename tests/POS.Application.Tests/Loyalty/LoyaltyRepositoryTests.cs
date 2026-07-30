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
        var saleReturnId = Guid.NewGuid();
        db.AddRange(LoyaltyTransaction.CreateCompensation(earned, "Return", saleReturnId: saleReturnId), LoyaltyTransaction.CreateCompensation(redeemed, "Return", saleReturnId: saleReturnId));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new LoyaltyRepository(db);
        Assert.Equal(LoyaltyTierTextForeground.White, (await repository.GetTiersAsync(TestContext.Current.CancellationToken)).Single().TextForeground);
        Assert.Equal(0, await repository.GetBalanceAsync(account.Id, TestContext.Current.CancellationToken));
        Assert.Equal(100, await repository.GetTotalIssuedPointsAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await repository.GetRedemptionCountAsync(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1), TestContext.Current.CancellationToken));

        db.ChangeTracker.Clear();
        db.LoyaltyTransactions.Add(LoyaltyTransaction.CreateCompensation(earned, "Duplicate", saleReturnId: saleReturnId));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BenefitCatalogue_RoundTrips_AndActiveOnlyHidesDeactivatedRewards()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var repository = new LoyaltyRepository(db);
        var advisory = new LoyaltyBenefit("Asesoría personalizada", 1_000, "Una hora con un asesor");
        var mug = new LoyaltyBenefit("Mug de regalo", 300);
        await repository.AddBenefitAsync(advisory, TestContext.Current.CancellationToken);
        await repository.AddBenefitAsync(mug, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<LoyaltyBenefit> all = await repository.GetBenefitsAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(["Mug de regalo", "Asesoría personalizada"], all.Select(benefit => benefit.Name));
        Assert.Equal("Una hora con un asesor", all.Single(benefit => benefit.PointsCost == 1_000).Description);

        LoyaltyBenefit tracked = await repository.GetBenefitByIdAsync(mug.Id, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("Benefit was not persisted.");
        tracked.Deactivate();
        await repository.UpdateBenefitAsync(tracked, TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<LoyaltyBenefit> active = await repository.GetBenefitsAsync(activeOnly: true, TestContext.Current.CancellationToken);
        Assert.Equal("Asesoría personalizada", active.Single().Name);
        Assert.Equal(2, (await repository.GetBenefitsAsync(cancellationToken: TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task BenefitClaim_SpendsFromTheBalance_WithoutDisturbingDiscountRedemptionMetrics()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var customer = new Customer("Ana Gomez", "123");
        var account = new LoyaltyAccount(customer.Id);
        var earned = new LoyaltyTransaction(account.Id, LoyaltyTransactionType.Earned, 2_500, Guid.NewGuid());
        var benefit = new LoyaltyBenefit("Asesoría personalizada", 1_000);
        db.AddRange(customer, account, earned, benefit);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new LoyaltyRepository(db);
        var spend = new LoyaltyTransaction(account.Id, LoyaltyTransactionType.BenefitRedeemed, -benefit.PointsCost, null, benefit.Name);
        await repository.AddTransactionAsync(spend, TestContext.Current.CancellationToken);
        await repository.AddClaimAsync(
            new LoyaltyBenefitClaim(benefit.Id, account.Id, benefit.Name, benefit.PointsCost, spend.Id),
            TestContext.Current.CancellationToken);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // The claim moves the balance...
        Assert.Equal(1_500, await repository.GetBalanceAsync(account.Id, TestContext.Current.CancellationToken));

        // ...but must not show up as a discount redemption, which is why BenefitRedeemed is its own type.
        Assert.Equal(0, await repository.GetRedemptionCountAsync(
            DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1), TestContext.Current.CancellationToken));
        Assert.Equal(2_500, await repository.GetTotalIssuedPointsAsync(TestContext.Current.CancellationToken));

        Assert.True(await repository.HasClaimsForBenefitAsync(benefit.Id, TestContext.Current.CancellationToken));
        LoyaltyBenefitClaim persisted = (await repository.GetClaimsByAccountAsync(account.Id, TestContext.Current.CancellationToken)).Single();
        Assert.Equal(LoyaltyBenefitClaimStatus.Pending, persisted.Status);
        Assert.Equal(1_000, persisted.PointsSpent);
    }

    [Fact]
    public async Task BenefitClaim_CannotBeRecordedTwiceAgainstTheSameSpendMovement()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var customer = new Customer("Ana Gomez", "123");
        var account = new LoyaltyAccount(customer.Id);
        var benefit = new LoyaltyBenefit("Asesoría personalizada", 1_000);
        var spend = new LoyaltyTransaction(account.Id, LoyaltyTransactionType.BenefitRedeemed, -1_000, null);
        db.AddRange(customer, account, benefit, spend);
        db.LoyaltyBenefitClaims.Add(new LoyaltyBenefitClaim(benefit.Id, account.Id, benefit.Name, 1_000, spend.Id));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.LoyaltyBenefitClaims.Add(new LoyaltyBenefitClaim(benefit.Id, account.Id, benefit.Name, 1_000, spend.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ClaimedBenefit_CannotBeDeletedFromTheCatalogue()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var customer = new Customer("Ana Gomez", "123");
        var account = new LoyaltyAccount(customer.Id);
        var benefit = new LoyaltyBenefit("Asesoría personalizada", 1_000);
        var spend = new LoyaltyTransaction(account.Id, LoyaltyTransactionType.BenefitRedeemed, -1_000, null);
        db.AddRange(customer, account, benefit, spend);
        db.LoyaltyBenefitClaims.Add(new LoyaltyBenefitClaim(benefit.Id, account.Id, benefit.Name, 1_000, spend.Id));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Restrict, not Cascade: deleting the reward would orphan the customer's claim history.
        // Cleared so the claim is untracked and the database FK is what rejects the delete — with the
        // claim still tracked, EF severs the required relationship and throws even earlier.
        // Either way the delete is refused, which is why DeleteLoyaltyBenefitUseCase must check
        // HasClaimsForBenefitAsync first and deactivate instead.
        db.ChangeTracker.Clear();
        var repository = new LoyaltyRepository(db);
        await repository.DeleteBenefitAsync(benefit.Id, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
