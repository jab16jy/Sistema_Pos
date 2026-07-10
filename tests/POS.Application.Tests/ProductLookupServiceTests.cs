using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Products;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Queries;
using Xunit;

namespace POS.Application.Tests;

public sealed class ProductLookupServiceTests
{
    [Fact]
    public async Task SearchAsync_MatchesNameIgnoringAccentsAndCase()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using AppDbContext dbContext = await SeedAsync(connection);
        var service = new ProductLookupService(dbContext);

        IReadOnlyList<ProductLookupResult> results = await service.SearchAsync("azucar", TestContext.Current.CancellationToken);

        ProductLookupResult result = Assert.Single(results);
        Assert.Equal("Azúcar Manuelita", result.Name);
    }

    [Fact]
    public async Task SearchAsync_SingleLetterQuery_ReturnsMatches()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using AppDbContext dbContext = await SeedAsync(connection);
        var service = new ProductLookupService(dbContext);

        IReadOnlyList<ProductLookupResult> results = await service.SearchAsync("a", TestContext.Current.CancellationToken);

        Assert.Contains(results, r => r.Name == "Azúcar Manuelita");
    }

    [Fact]
    public async Task SearchAsync_MatchesByCategoryName()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using AppDbContext dbContext = await SeedAsync(connection);
        var service = new ProductLookupService(dbContext);

        IReadOnlyList<ProductLookupResult> results = await service.SearchAsync("granos", TestContext.Current.CancellationToken);

        ProductLookupResult result = Assert.Single(results);
        Assert.Equal("Azúcar Manuelita", result.Name);
    }

    [Fact]
    public async Task SearchAsync_MatchesByExactPrice()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using AppDbContext dbContext = await SeedAsync(connection);
        var service = new ProductLookupService(dbContext);

        IReadOnlyList<ProductLookupResult> results = await service.SearchAsync("4500", TestContext.Current.CancellationToken);

        ProductLookupResult result = Assert.Single(results);
        Assert.Equal(4_500m, result.UnitPrice);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmpty()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using AppDbContext dbContext = await SeedAsync(connection);
        var service = new ProductLookupService(dbContext);

        Assert.Empty(await service.SearchAsync("   ", TestContext.Current.CancellationToken));
        Assert.Empty(await service.SearchAsync(string.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProductStorage_RejectsDuplicateNonNullBarcodes()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var category = new Category("Granos");
        await dbContext.Categories.AddAsync(category, TestContext.Current.CancellationToken);
        await dbContext.Products.AddAsync(new Product("Rice", category.Id, new Money(1m), new Money(2m), new StockQuantity(1m), new StockQuantity(0m), barcode: new Barcode("RICE-001")), TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await dbContext.Products.AddAsync(new Product("Beans", category.Id, new Money(1m), new Money(2m), new StockQuantity(1m), new StockQuantity(0m), barcode: new Barcode("RICE-001")), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static async Task<AppDbContext> SeedAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var category = new Category("Granos");
        await dbContext.Categories.AddAsync(category, TestContext.Current.CancellationToken);

        // Explicit barcodes keep SourceCode deterministic (no random MANUAL-{guid} noise in matches)
        var sugar = new Product("Azúcar Manuelita", category.Id, new Money(3_000m), new Money(4_500m), new StockQuantity(10m), new StockQuantity(2m), barcode: new Barcode("7701001"));
        var soap = new Product("Jabón Rey", Guid.NewGuid(), new Money(1_000m), new Money(2_000m), new StockQuantity(5m), new StockQuantity(1m), barcode: new Barcode("7701002"));
        await dbContext.Products.AddRangeAsync([sugar, soap], TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return dbContext;
    }
}
