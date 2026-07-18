using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests.Persistence;

public sealed class IdentityReceivableMigrationTests
{
    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        await cmd.ExecuteNonQueryAsync();
        return connection;
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Migration_CreatesRestrictiveForeignKeys_AndRejectsInvalidActorOrReceivableWrites()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        // 1. Invalid Sale with missing CashierUserId fails at DB boundary
        var invalidSale = new Sale(Guid.NewGuid());
        dbContext.Sales.Add(invalidSale);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();

        // 2. Invalid SaleReturn with missing ProcessedByUserId fails at DB boundary
        var invalidReturn = new SaleReturn(Guid.NewGuid(), Guid.NewGuid(), "Reason");
        dbContext.SaleReturns.Add(invalidReturn);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();

        // 3. Invalid CashierShift with missing CashierUserId fails at DB boundary
        var invalidShift = new CashierShift(Guid.NewGuid(), "user", new Money(100m));
        dbContext.CashierShifts.Add(invalidShift);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();

        // 4. Invalid Receivable with missing CustomerId / SaleId fails at DB boundary
        var invalidReceivable = new Receivable(Guid.NewGuid(), Guid.NewGuid(), new Money(500m));
        dbContext.Receivables.Add(invalidReceivable);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task ValidLegacyData_MigratesUnchanged_AndAllowsValidWrites()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        // Seed valid principal User
        var role = Role.CreateSystemRole(Guid.NewGuid(), "Admin");
        dbContext.Roles.Add(role);
        var user = new User("validcashier", "hash", role.Id);
        dbContext.Users.Add(user);

        // Seed valid principal Customer
        var customer = new Customer("John Doe", "12345678");
        dbContext.Customers.Add(customer);

        await dbContext.SaveChangesAsync();

        // Valid Sale with valid CashierUserId
        var validSale = new Sale(user.Id);
        dbContext.Sales.Add(validSale);
        await dbContext.SaveChangesAsync();

        // Valid SaleReturn with valid ProcessedByUserId
        var validReturn = new SaleReturn(validSale.Id, user.Id, "Customer return");
        dbContext.SaleReturns.Add(validReturn);
        await dbContext.SaveChangesAsync();

        // Valid CashierShift with valid CashierUserId
        var validShift = new CashierShift(user.Id, user.Username, new Money(50m));
        dbContext.CashierShifts.Add(validShift);
        await dbContext.SaveChangesAsync();

        // Valid Receivable with valid CustomerId and SaleId
        var validReceivable = new Receivable(customer.Id, validSale.Id, new Money(100m));
        dbContext.Receivables.Add(validReceivable);
        await dbContext.SaveChangesAsync();

        Assert.NotNull(await dbContext.Sales.FindAsync(validSale.Id));
        Assert.NotNull(await dbContext.SaleReturns.FindAsync(validReturn.Id));
        Assert.NotNull(await dbContext.CashierShifts.FindAsync(validShift.Id));
        Assert.NotNull(await dbContext.Receivables.FindAsync(validReceivable.Id));
    }

    [Fact]
    public async Task PreExistingOrphanIdentityAndReceivableReferences_AreCleanedUp_WhenAddForeignKeysMigrationRuns()
    {
        const string MigrationBeforeIdentityForeignKeys = "20260716183752_AddUniqueIndexToUsername";

        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.GetService<IMigrator>().MigrateAsync(MigrationBeforeIdentityForeignKeys);

        // Seed valid customer
        var customer = new Customer("Valid Customer", "12345");
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        // Seed orphaned records pointing to non-existent User IDs, Customer IDs, or Sale IDs
        Guid missingUserId = Guid.NewGuid();
        Guid missingSaleId = Guid.NewGuid();
        Guid missingCustomerId = Guid.NewGuid();

        // Orphan Sale (missing CashierUserId)
        await dbContext.Database.ExecuteSqlRawAsync($"""
            INSERT INTO "SALES" ("Id", "InvoiceNumber", "CashierUserId", "Status", "CreatedAt")
            VALUES ('{Guid.NewGuid()}', 'INV-TEST-001', '{missingUserId}', 3, '2026-01-01');
            """);

        // Orphan SaleReturn (missing ProcessedByUserId)
        await dbContext.Database.ExecuteSqlRawAsync($"""
            INSERT INTO "SALE_RETURNS" ("Id", "OriginalSaleId", "ProcessedByUserId", "Reason", "ReturnedAt")
            VALUES ('{Guid.NewGuid()}', '{missingSaleId}', '{missingUserId}', 'Orphan return', '2026-01-01');
            """);

        // Orphan CashierShift (missing CashierUserId)
        await dbContext.Database.ExecuteSqlRawAsync($"""
            INSERT INTO "CASHIER_SHIFTS" ("Id", "CashierUserId", "CashierUsername", "opening_cash", "closing_cash", "Status", "OpenedAt")
            VALUES ('{Guid.NewGuid()}', '{missingUserId}', 'ghost', 100, 0, 0, '2026-01-01');
            """);

        // Orphan Receivable (missing CustomerId and SaleId)
        await dbContext.Database.ExecuteSqlRawAsync($"""
            INSERT INTO "RECEIVABLES" ("Id", "CustomerId", "SaleId", "original_balance", "outstanding_balance", "is_cancelled", "version", "CreatedAt")
            VALUES ('{Guid.NewGuid()}', '{missingCustomerId}', '{missingSaleId}', 100, 100, 0, 0, '2026-01-01');
            """);

        // Migrate past AddIdentityAndReceivableForeignKeys — should clean up dangling rows instead of throwing FK failure
        await dbContext.Database.MigrateAsync();

        Assert.Equal(0, await dbContext.Sales.CountAsync());
        Assert.Equal(0, await dbContext.SaleReturns.CountAsync());
        Assert.Equal(0, await dbContext.CashierShifts.CountAsync());
        Assert.Equal(0, await dbContext.Receivables.CountAsync());
    }
}
