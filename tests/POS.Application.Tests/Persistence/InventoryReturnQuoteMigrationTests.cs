using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Preflight;
using Xunit;

namespace POS.Application.Tests.Persistence;

public sealed class InventoryReturnQuoteMigrationTests
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

    private static async Task<(User Cashier, Product Product)> SeedActorAndProductAsync(AppDbContext dbContext)
    {
        var role = Role.CreateSystemRole(Guid.NewGuid(), "Admin");
        dbContext.Roles.Add(role);
        var cashier = new User("cashier1", "hash", role.Id);
        dbContext.Users.Add(cashier);

        var category = new Category("General");
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        // Seeded through raw SQL so the rewound schema decides the columns, not the current model.
        var product = new Product("Widget", category.Id, new Money(10m), new Money(20m), new StockQuantity(50m), new StockQuantity(1m));
        await LegacyProductRow.InsertAsync(dbContext, product);

        return (cashier, product);
    }

    [Fact]
    public async Task Migration_CreatesRestrictiveForeignKeys_AndRejectsOrphanWrites()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var (cashier, product) = await SeedActorAndProductAsync(dbContext);

        // 1. InventoryMovement with an unknown ProductId fails at the DB boundary.
        var invalidMovement = new InventoryMovement(Guid.NewGuid(), InventoryMovementType.Entry, new Quantity(1m), "Adjustment", cashier.Id);
        dbContext.InventoryMovements.Add(invalidMovement);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();

        // 2. SaleReturnItem with an unknown SaleItemId fails at the DB boundary.
        var sale = new Sale(cashier.Id);
        sale.AddItem(product, new Quantity(1m), Money.Zero);
        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var saleReturn = new SaleReturn(sale.Id, cashier.Id, "Reason", Guid.NewGuid());
        var invalidReturnItem = new SaleReturnItem(saleReturn.Id, Guid.NewGuid(), product.Id, product.Name, 1, new Money(20m), new Money(20m));
        saleReturn.AddItem(invalidReturnItem);
        dbContext.SaleReturns.Add(saleReturn);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();

        // 3. SaleReturnItem with an unknown ProductId fails at the DB boundary.
        var saleItemId = sale.Items.Single().Id;
        var saleReturn2 = new SaleReturn(sale.Id, cashier.Id, "Reason", Guid.NewGuid());
        var invalidProductReturnItem = new SaleReturnItem(saleReturn2.Id, saleItemId, Guid.NewGuid(), "Ghost product", 1, new Money(20m), new Money(20m));
        saleReturn2.AddItem(invalidProductReturnItem);
        dbContext.SaleReturns.Add(saleReturn2);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();

        // 4. Quote with an unknown CustomerId fails at the DB boundary.
        var invalidCustomerQuote = new Quote("COT-BAD-1", "Ghost", 30, Guid.NewGuid());
        dbContext.Quotes.Add(invalidCustomerQuote);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();

        // 5. Quote with an unknown ConvertedSaleId fails at the DB boundary.
        var invalidConvertedQuote = new Quote("COT-BAD-2", "Someone", 30);
        invalidConvertedQuote.Convert(Guid.NewGuid());
        dbContext.Quotes.Add(invalidConvertedQuote);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task ValidLegacyData_MigratesUnchanged_AndAllowsValidWrites()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        var (cashier, product) = await SeedActorAndProductAsync(dbContext);
        var customer = new Customer("Jane Doe", "87654321");
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();

        // Valid InventoryMovement referencing a real product.
        var movement = new InventoryMovement(product.Id, InventoryMovementType.Entry, new Quantity(3m), "Restock", cashier.Id);
        dbContext.InventoryMovements.Add(movement);
        await dbContext.SaveChangesAsync();

        // Valid SaleReturnItem referencing a real sale item and product.
        var sale = new Sale(cashier.Id, customer.Id);
        sale.AddItem(product, new Quantity(2m), Money.Zero);
        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync();

        var saleItem = sale.Items.Single();
        var saleReturn = new SaleReturn(sale.Id, cashier.Id, "Customer changed mind", Guid.NewGuid());
        saleReturn.AddItem(new SaleReturnItem(saleReturn.Id, saleItem.Id, product.Id, product.Name, 1, saleItem.UnitPrice, saleItem.UnitPrice));
        dbContext.SaleReturns.Add(saleReturn);
        await dbContext.SaveChangesAsync();

        // Valid Quote referencing a real customer and, once converted, a real sale.
        var quote = new Quote("COT-OK-1", customer.FullName, 30, customer.Id);
        quote.AddItem(new QuoteItem(product.Id, product.Name, new Money(20m), new Quantity(1m), 0m));
        dbContext.Quotes.Add(quote);
        await dbContext.SaveChangesAsync();

        quote.Convert(sale.Id);
        await dbContext.SaveChangesAsync();

        Assert.NotNull(await dbContext.InventoryMovements.FindAsync(movement.Id));
        Assert.NotNull(await dbContext.SaleReturnItems.FindAsync(saleReturn.Items.Single().Id));
        Assert.Equal(sale.Id, (await dbContext.Quotes.FindAsync(quote.Id))!.ConvertedSaleId);

        var preflight = await IntegrityPreflight.CheckAsync(dbContext);
        Assert.True(preflight.IsValid, string.Join("; ", preflight.Errors));
    }

    [Fact]
    public async Task PreExistingOrphanReferences_AreCleanedUp_WhenAddForeignKeysMigrationRuns()
    {
        // Reproduces the real crash: a database that already has rows pointing at deleted
        // principals (e.g. from a product reseed/reset done outside the app) BEFORE this
        // migration ever runs. AddForeignKey forces SQLite to rebuild each table, which used
        // to abort with "FOREIGN KEY constraint failed" instead of migrating past stale rows.
        const string MigrationBeforeForeignKeys = "20260718004341_AddIdentityAndReceivableForeignKeys";

        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.GetService<IMigrator>().MigrateAsync(MigrationBeforeForeignKeys);

        var (cashier, product) = await SeedActorAndProductAsync(dbContext);

        // Orphan InventoryMovement: ProductId points at a product that no longer exists.
        var orphanMovement = new InventoryMovement(Guid.NewGuid(), InventoryMovementType.Entry, new Quantity(1m), "Orphan movement", cashier.Id);
        dbContext.InventoryMovements.Add(orphanMovement);

        // Orphan SaleReturnItem: ProductId and SaleItemId both point at rows that no longer exist.
        var sale = new Sale(cashier.Id);
        sale.AddItem(product, new Quantity(1m), Money.Zero);
        dbContext.Sales.Add(sale);
        var saleReturn = new SaleReturn(sale.Id, cashier.Id, "Orphan return", Guid.NewGuid());
        var orphanReturnItem = new SaleReturnItem(saleReturn.Id, Guid.NewGuid(), Guid.NewGuid(), "Ghost product", 1, new Money(20m), new Money(20m));
        saleReturn.AddItem(orphanReturnItem);
        dbContext.SaleReturns.Add(saleReturn);

        // Orphan Quote: CustomerId and ConvertedSaleId both point at rows that no longer exist.
        var orphanQuote = new Quote("COT-ORPHAN-1", "Ghost customer", 30, Guid.NewGuid());
        orphanQuote.Convert(Guid.NewGuid());
        dbContext.Quotes.Add(orphanQuote);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // The rest of the chain must apply without throwing, cleaning up the dangling
        // references instead of crashing the SQLite table rebuild that AddForeignKey triggers.
        await dbContext.Database.MigrateAsync();

        Assert.Null(await dbContext.InventoryMovements.FindAsync(orphanMovement.Id));
        Assert.Null(await dbContext.SaleReturnItems.FindAsync(orphanReturnItem.Id));

        Quote persistedQuote = await dbContext.Quotes.SingleAsync(q => q.Id == orphanQuote.Id);
        Assert.Null(persistedQuote.CustomerId);
        Assert.Null(persistedQuote.ConvertedSaleId);
    }

    [Fact]
    public async Task Preflight_WithOrphanInventoryProductReference_FailsBeforeSchemaMutation()
    {
        // Simulates re-running preflight against a legacy database that still has an orphan row:
        // the gate must catch it (and therefore block DatabaseSchemaRollout from calling MigrateAsync)
        // without this test itself mutating the FK-enforced schema.
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync();

        string productId = Guid.NewGuid().ToString();
        string movementId = Guid.NewGuid().ToString();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = OFF;";
            await cmd.ExecuteNonQueryAsync();
            cmd.CommandText = $"INSERT INTO \"InventoryMovements\" (\"Id\", \"ProductId\", \"Type\", \"Quantity\", \"Reason\", \"UserId\", \"CreatedAt\") VALUES ('{movementId}', '{productId}', 0, 1, 'Orphan', '{Guid.NewGuid()}', '2026-01-01');";
            await cmd.ExecuteNonQueryAsync();
        }

        var result = await IntegrityPreflight.CheckAsync(dbContext);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("InventoryMovements") && e.Contains("ProductId -> PRODUCTS.Id"));
    }
}
