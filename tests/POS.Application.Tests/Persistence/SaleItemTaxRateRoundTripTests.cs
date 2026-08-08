using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests.Persistence;

/// <summary>
/// Regression test for the EF Core "CLR-default skip" bug: SaleItemConfiguration used to declare
/// .HasDefaultValue(19m) on a non-nullable decimal column, which makes EF Core treat any in-memory
/// value equal to default(decimal) (0m) as "unset" and omit it from the INSERT, letting the DB's
/// column default silently overwrite IVA-exempt (0%) sale items with 19. A plain domain-level unit
/// test can't catch this — it only reproduces against a real SaveChanges/reload round trip.
/// </summary>
public sealed class SaleItemTaxRateRoundTripTests
{
    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
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
    public async Task SaleItem_WithZeroPercentTaxRate_PersistsAsZero_NotDefault()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);

        var category = new Category("General");
        dbContext.Categories.Add(category);
        var exemptProduct = new Product("Exempt widget", category.Id, new Money(10m), new Money(20m), new StockQuantity(50m), new StockQuantity(1m), taxRate: 0m);
        var taxedProduct = new Product("Taxed widget", category.Id, new Money(10m), new Money(20m), new StockQuantity(50m), new StockQuantity(1m), taxRate: 5m);
        dbContext.Products.Add(exemptProduct);
        dbContext.Products.Add(taxedProduct);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var cashierRole = Role.CreateSystemRole(Guid.NewGuid(), "Admin");
        dbContext.Roles.Add(cashierRole);
        var cashier = new User("cashier1", "hash", cashierRole.Id);
        dbContext.Users.Add(cashier);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sale = new Sale(cashier.Id);
        sale.AddItem(exemptProduct, new Quantity(1m), Money.Zero);
        sale.AddItem(taxedProduct, new Quantity(1m), Money.Zero);
        dbContext.Sales.Add(sale);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var reloadContext = CreateDbContext(connection);
        var reloadedSale = await reloadContext.Sales
            .Include(s => s.Items)
            .SingleAsync(s => s.Id == sale.Id, TestContext.Current.CancellationToken);

        Assert.Equal(0m, reloadedSale.Items.Single(i => i.ProductId == exemptProduct.Id).TaxRate);
        Assert.Equal(5m, reloadedSale.Items.Single(i => i.ProductId == taxedProduct.Id).TaxRate);
    }
}
