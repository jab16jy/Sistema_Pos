using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests.Invoices;

public sealed class ReturnResolutionPersistenceTests
{
    [Fact]
    public async Task ReturnSettlement_WithValidReturn_Persists()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        SaleReturn saleReturn = await AddReturnAsync(dbContext);
        var settlement = new ReturnSettlement(saleReturn.Id, new Money(12_000m), PaymentMethod.Cash);

        dbContext.Set<ReturnSettlement>().Add(settlement);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        ReturnSettlement persisted = await dbContext.Set<ReturnSettlement>()
            .SingleAsync(item => item.Id == settlement.Id, TestContext.Current.CancellationToken);
        Assert.Equal(saleReturn.Id, persisted.SaleReturnId);
        Assert.Equal(12_000m, persisted.Amount.Amount);
    }

    [Fact]
    public async Task ReturnReceivableReconciliation_WithValidReturnAndReceivable_Persists()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        SaleReturn saleReturn = await AddReturnAsync(dbContext);
        Receivable receivable = await AddReceivableAsync(dbContext);
        var reconciliation = new ReturnReceivableReconciliation(saleReturn.Id, receivable.Id, new Money(12_000m));

        dbContext.Set<ReturnReceivableReconciliation>().Add(reconciliation);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        ReturnReceivableReconciliation persisted = await dbContext.Set<ReturnReceivableReconciliation>()
            .SingleAsync(item => item.Id == reconciliation.Id, TestContext.Current.CancellationToken);
        Assert.Equal(receivable.Id, persisted.ReceivableId);
    }

    [Fact]
    public async Task ReturnResolutions_WithDuplicateReturnReference_AreRejected()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        SaleReturn saleReturn = await AddReturnAsync(dbContext);
        dbContext.Set<ReturnSettlement>().Add(new ReturnSettlement(saleReturn.Id, new Money(12_000m), PaymentMethod.Cash));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        dbContext.ChangeTracker.Clear();
        dbContext.Set<ReturnSettlement>().Add(new ReturnSettlement(saleReturn.Id, new Money(12_000m), PaymentMethod.DebitCard));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReturnReconciliations_WithDuplicateReturnReference_AreRejected()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        SaleReturn saleReturn = await AddReturnAsync(dbContext);
        Receivable receivable = await AddReceivableAsync(dbContext);
        dbContext.Set<ReturnReceivableReconciliation>().Add(
            new ReturnReceivableReconciliation(saleReturn.Id, receivable.Id, new Money(12_000m)));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        dbContext.ChangeTracker.Clear();
        dbContext.Set<ReturnReceivableReconciliation>().Add(
            new ReturnReceivableReconciliation(saleReturn.Id, receivable.Id, new Money(12_000m)));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReturnResolution_WithMissingPrincipal_IsRejected()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        dbContext.Set<ReturnSettlement>().Add(new ReturnSettlement(Guid.NewGuid(), new Money(12_000m), PaymentMethod.Cash));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReturnReconciliation_WithMissingReceivable_IsRejected()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        SaleReturn saleReturn = await AddReturnAsync(dbContext);
        dbContext.Set<ReturnReceivableReconciliation>().Add(
            new ReturnReceivableReconciliation(saleReturn.Id, Guid.NewGuid(), new Money(12_000m)));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReturnOperationIds_AreUniqueWhenPresent()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Guid operationId = Guid.NewGuid();
        Guid processedByUserId = await TestCashierFactory.CreateAndPersistAsync(dbContext, TestContext.Current.CancellationToken);
        dbContext.SaleReturns.Add(new SaleReturn(Guid.NewGuid(), processedByUserId, "Customer requested a return.", operationId));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        dbContext.SaleReturns.Add(new SaleReturn(Guid.NewGuid(), processedByUserId, "Customer requested a return.", operationId));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LegacyReturn_WithoutResolution_RemainsReadable()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        SaleReturn saleReturn = await AddReturnAsync(dbContext);
        await AddReturnAsync(dbContext);
        dbContext.ChangeTracker.Clear();

        SaleReturn persisted = await dbContext.SaleReturns.SingleAsync(item => item.Id == saleReturn.Id, TestContext.Current.CancellationToken);
        Assert.Null(persisted.OperationId);
        Assert.Equal(2, await dbContext.SaleReturns.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await dbContext.Set<ReturnSettlement>().ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await dbContext.Set<ReturnReceivableReconciliation>().ToListAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<SaleReturn> AddReturnAsync(AppDbContext dbContext)
    {
        Role? role = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", TestContext.Current.CancellationToken);
        if (role is null)
        {
            role = Role.CreateSystemRole(Guid.NewGuid(), "Admin");
            dbContext.Roles.Add(role);
        }
        var user = new User("retuser_" + Guid.NewGuid().ToString("N")[..8], "hash", role.Id);
        dbContext.Users.Add(user);

        var saleReturn = new SaleReturn(Guid.NewGuid(), user.Id, "Customer requested a return.");
        dbContext.SaleReturns.Add(saleReturn);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return saleReturn;
    }

    private static async Task<Receivable> AddReceivableAsync(AppDbContext dbContext)
    {
        var customer = new Customer("Test Customer", "12345678");
        dbContext.Customers.Add(customer);

        Role? role = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", TestContext.Current.CancellationToken);
        if (role is null)
        {
            role = Role.CreateSystemRole(Guid.NewGuid(), "Admin");
            dbContext.Roles.Add(role);
        }
        var user = new User("recuser_" + Guid.NewGuid().ToString("N")[..8], "hash", role.Id);
        dbContext.Users.Add(user);

        var sale = new Sale(user.Id);
        dbContext.Sales.Add(sale);

        var receivable = new Receivable(customer.Id, sale.Id, new Money(12_000m));
        dbContext.Receivables.Add(receivable);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return receivable;
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
