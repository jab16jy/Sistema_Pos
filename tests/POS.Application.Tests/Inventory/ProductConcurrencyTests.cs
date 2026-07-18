using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests.Inventory;

public sealed class ProductConcurrencyTests
{
    [Fact]
    public async Task TwoContexts_UpdatingSameProduct_ThrowsConcurrencyConflictException_AndFirstUpdateSurvives()
    {
        await using var connection = await OpenConnectionAsync();
        await using var initContext = CreateDbContext(connection);
        await initContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var category = new Category("General");
        initContext.Categories.Add(category);
        var product = new Product(
            name: "Test Product",
            categoryId: category.Id,
            costPrice: new Money(10m),
            salePrice: new Money(15m),
            currentStock: new StockQuantity(100m),
            minimumStock: new StockQuantity(10m));
        initContext.Products.Add(product);
        await initContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, product.Version);

        await using var dbContext1 = CreateDbContext(connection);
        await using var dbContext2 = CreateDbContext(connection);

        var product1 = await dbContext1.Products.SingleAsync(p => p.Id == product.Id, TestContext.Current.CancellationToken);
        var product2 = await dbContext2.Products.SingleAsync(p => p.Id == product.Id, TestContext.Current.CancellationToken);

        var movement1 = product1.RegisterStockMovement(InventoryMovementType.Exit, -10m, "Sale 1", Guid.NewGuid());
        dbContext1.InventoryMovements.Add(movement1);
        await dbContext1.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, product1.Version);

        var movement2 = product2.RegisterStockMovement(InventoryMovementType.Exit, -20m, "Sale 2", Guid.NewGuid());
        dbContext2.InventoryMovements.Add(movement2);

        var exception = await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            dbContext2.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.NotNull(exception.InnerException);
        Assert.IsType<DbUpdateConcurrencyException>(exception.InnerException);

        await using var verifyContext = CreateDbContext(connection);
        var refreshedProduct = await verifyContext.Products.SingleAsync(p => p.Id == product.Id, TestContext.Current.CancellationToken);
        Assert.Equal(90m, refreshedProduct.CurrentStock);
        Assert.Equal(1, refreshedProduct.Version);

        var movements = await verifyContext.InventoryMovements.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Single(movements);
        Assert.Equal("Sale 1", movements[0].Reason);
    }

    [Fact]
    public async Task StaleStockWrite_ClearsChangeTracker_AndWritesNoOutcome()
    {
        await using var connection = await OpenConnectionAsync();
        await using var initContext = CreateDbContext(connection);
        await initContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var category = new Category("General");
        initContext.Categories.Add(category);
        var product = new Product(
            name: "Stale Product",
            categoryId: category.Id,
            costPrice: new Money(5m),
            salePrice: new Money(10m),
            currentStock: new StockQuantity(50m),
            minimumStock: new StockQuantity(5m));
        initContext.Products.Add(product);
        await initContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var dbContext1 = CreateDbContext(connection);
        await using var dbContext2 = CreateDbContext(connection);

        var product1 = await dbContext1.Products.SingleAsync(p => p.Id == product.Id, TestContext.Current.CancellationToken);
        var product2 = await dbContext2.Products.SingleAsync(p => p.Id == product.Id, TestContext.Current.CancellationToken);

        product1.DecreaseStock(new Quantity(5m));
        await dbContext1.SaveChangesAsync(TestContext.Current.CancellationToken);

        product2.DecreaseStock(new Quantity(15m));
        var movement = new InventoryMovement(product2.Id, InventoryMovementType.Exit, new Quantity(15m), "Stale exit", Guid.NewGuid());
        dbContext2.InventoryMovements.Add(movement);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            dbContext2.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Empty(dbContext2.ChangeTracker.Entries());

        await using var verifyContext = CreateDbContext(connection);
        var persistedProduct = await verifyContext.Products.SingleAsync(p => p.Id == product.Id, TestContext.Current.CancellationToken);
        Assert.Equal(45m, persistedProduct.CurrentStock);
        Assert.Empty(await verifyContext.InventoryMovements.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BusyOrLockedDatabase_ThrowsTransactionConflictException_NotConcurrencyConflictException()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var busyException = new SqliteException("database is locked", 5);

        await Assert.ThrowsAsync<TransactionConflictException>(() =>
            dbContext.ExecuteInTransactionAsync(_ => throw busyException, TestContext.Current.CancellationToken));
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
