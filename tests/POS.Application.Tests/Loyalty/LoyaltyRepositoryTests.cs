using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests.Loyalty;

public sealed class LoyaltyRepositoryTests
{
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
}
