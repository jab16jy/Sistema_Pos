using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests.Loyalty;

public sealed class LoyaltyReturnPersistenceTests
{
    [Fact]
    public async Task Compensations_ForSameSourceAndDistinctReturns_Persist()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        LoyaltyTransaction source = await AddSourceTransactionAsync(dbContext);
        SaleReturn firstReturn = await AddReturnAsync(dbContext);
        SaleReturn secondReturn = await AddReturnAsync(dbContext);

        var firstCompensation = LoyaltyTransaction.CreateCompensation(source, "Partial return.", 3, firstReturn.Id);
        var secondCompensation = LoyaltyTransaction.CreateCompensation(source, "Later partial return.", 4, secondReturn.Id);
        dbContext.LoyaltyTransactions.AddRange(firstCompensation, secondCompensation);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<LoyaltyTransaction> persisted = (await dbContext.LoyaltyTransactions
            .Where(transaction => transaction.CompensatesTransactionId == source.Id)
            .ToListAsync(TestContext.Current.CancellationToken))
            .OrderBy(transaction => transaction.CreatedAt)
            .ToList();

        Assert.Collection(
            persisted,
            transaction => Assert.Equal(firstReturn.Id, transaction.SaleReturnId),
            transaction => Assert.Equal(secondReturn.Id, transaction.SaleReturnId));
    }

    [Fact]
    public async Task Compensations_WithSameSourceAndReturn_AreRejected()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        LoyaltyTransaction source = await AddSourceTransactionAsync(dbContext);
        SaleReturn saleReturn = await AddReturnAsync(dbContext);
        dbContext.LoyaltyTransactions.Add(LoyaltyTransaction.CreateCompensation(source, "Partial return.", 3, saleReturn.Id));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        dbContext.LoyaltyTransactions.Add(LoyaltyTransaction.CreateCompensation(source, "Repeated return.", 1, saleReturn.Id));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LegacyCompensation_WithoutReturnLink_RemainsReadable()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        LoyaltyTransaction source = await AddSourceTransactionAsync(dbContext);
        LoyaltyTransaction legacyCompensation = LoyaltyTransaction.CreateCompensation(source, "Historical compensation.", 3);
        dbContext.LoyaltyTransactions.Add(legacyCompensation);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        dbContext.ChangeTracker.Clear();

        LoyaltyTransaction persisted = await dbContext.LoyaltyTransactions
            .SingleAsync(transaction => transaction.Id == legacyCompensation.Id, TestContext.Current.CancellationToken);

        Assert.Equal(source.Id, persisted.CompensatesTransactionId);
        Assert.Null(persisted.SaleReturnId);
    }

    private static async Task<LoyaltyTransaction> AddSourceTransactionAsync(AppDbContext dbContext)
    {
        var customer = new Customer("Loyalty Customer", Guid.NewGuid().ToString("N"));
        var account = new LoyaltyAccount(customer.Id);
        dbContext.AddRange(customer, account);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var source = new LoyaltyTransaction(account.Id, LoyaltyTransactionType.Earned, 10, Guid.NewGuid(), "Original earning.");
        dbContext.LoyaltyTransactions.Add(source);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return source;
    }

    private static async Task<SaleReturn> AddReturnAsync(AppDbContext dbContext)
    {
        Role? role = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", TestContext.Current.CancellationToken);
        if (role is null)
        {
            role = Role.CreateSystemRole(Guid.NewGuid(), "Admin");
            dbContext.Roles.Add(role);
        }
        var user = new User("loyuser_" + Guid.NewGuid().ToString("N")[..8], "hash", role.Id);
        dbContext.Users.Add(user);

        var saleReturn = new SaleReturn(Guid.NewGuid(), user.Id, "Customer requested a return.");
        dbContext.SaleReturns.Add(saleReturn);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return saleReturn;
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
}
