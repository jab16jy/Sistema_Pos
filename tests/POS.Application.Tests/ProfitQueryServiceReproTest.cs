using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Queries;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests;

public sealed class ProfitQueryServiceReproTest
{
    [Fact]
    public async Task GetTopSellersAsync_RunsAgainstRealSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var product = new Product("Widget", Guid.NewGuid(), new Money(10_000m), new Money(50_000m), new StockQuantity(20m), new StockQuantity(2m));
        await dbContext.Products.AddAsync(product, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(2m), Money.Zero);
        sale.AddPayment(new Domain.Entities.Payment(Domain.Enums.PaymentMethod.Cash, new Money(100_000m)));
        sale.Complete();
        await dbContext.Sales.AddAsync(sale, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new ProfitQueryService(dbContext);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var ex = await Record.ExceptionAsync(() => service.GetTopSellersAsync(today.AddDays(-1), today.AddDays(1), 10, TestContext.Current.CancellationToken));
        Assert.Null(ex);
    }

    [Fact]
    public async Task GetPeriodComparisonAsync_RunsAgainstRealSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var product = new Product("Widget", Guid.NewGuid(), new Money(10_000m), new Money(50_000m), new StockQuantity(20m), new StockQuantity(2m));
        await dbContext.Products.AddAsync(product, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(2m), Money.Zero);
        sale.AddPayment(new Domain.Entities.Payment(Domain.Enums.PaymentMethod.Cash, new Money(100_000m)));
        sale.Complete();
        await dbContext.Sales.AddAsync(sale, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new ProfitQueryService(dbContext);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var ex = await Record.ExceptionAsync(() => service.GetPeriodComparisonAsync(today.AddDays(-1), today.AddDays(1), today.AddDays(-3), today.AddDays(-2), TestContext.Current.CancellationToken));
        Assert.Null(ex);
    }

    [Fact]
    public async Task GetPlatformBreakdownAsync_RunsAgainstRealSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var product = new Product("Widget", Guid.NewGuid(), new Money(10_000m), new Money(50_000m), new StockQuantity(20m), new StockQuantity(2m));
        await dbContext.Products.AddAsync(product, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(2m), Money.Zero);
        sale.AddPayment(new Domain.Entities.Payment(Domain.Enums.PaymentMethod.Cash, new Money(100_000m)));
        sale.Complete();
        await dbContext.Sales.AddAsync(sale, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new ProfitQueryService(dbContext);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var ex = await Record.ExceptionAsync(() => service.GetPlatformBreakdownAsync(today.AddDays(-1), today.AddDays(1), TestContext.Current.CancellationToken));
        Assert.Null(ex);
    }
}
