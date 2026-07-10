using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using POS.Application.DTOs.Products;
using POS.Application.UseCases.Products;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Queries;
using POS.Infrastructure.Persistence.Repositories;
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
    public async Task FindByBarcodeAsync_ReturnsOnlyTheActiveExactMatchBeyondSearchLimit()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var category = new Category("Scanner");
        await dbContext.Categories.AddAsync(category, TestContext.Current.CancellationToken);
        var fuzzyCandidates = Enumerable.Range(0, 20)
            .Select(index => new Product(
                $"A candidate {index:D2}",
                category.Id,
                new Money(1m),
                new Money(2m),
                new StockQuantity(1m),
                new StockQuantity(0m),
                barcode: new Barcode($"SCAN-EXACT-{index:D2}")))
            .ToArray();
        var exactProduct = new Product("Z exact scanner product", category.Id, new Money(1m), new Money(2m), new StockQuantity(1m), new StockQuantity(0m), barcode: new Barcode("SCAN-EXACT"));
        var inactiveProduct = new Product("Inactive scanner product", category.Id, new Money(1m), new Money(2m), new StockQuantity(1m), new StockQuantity(0m), barcode: new Barcode("INACTIVE-SCAN"));
        inactiveProduct.Deactivate();
        await dbContext.Products.AddRangeAsync([.. fuzzyCandidates, exactProduct, inactiveProduct], TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new ProductLookupService(dbContext);
        IReadOnlyList<ProductLookupResult> fuzzyResults = await service.SearchAsync("SCAN-EXACT", TestContext.Current.CancellationToken);
        ProductLookupResult? exactResult = await service.FindByBarcodeAsync("SCAN-EXACT", TestContext.Current.CancellationToken);

        Assert.Equal(20, fuzzyResults.Count);
        Assert.DoesNotContain(fuzzyResults, result => result.ProductId == exactProduct.Id);
        Assert.NotNull(exactResult);
        Assert.Equal(exactProduct.Id, exactResult.ProductId);
        Assert.Null(await service.FindByBarcodeAsync("INACTIVE-SCAN", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EnforceProductBarcodeUniquenessMigration_UpgradesDuplicateAndBlankRows()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        await using var dbContext = new AppDbContext(options);
        IMigrator migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260710054026_AddProductBoxPresentation", TestContext.Current.CancellationToken);

        var category = new Category("Migration");
        await dbContext.Categories.AddAsync(category, TestContext.Current.CancellationToken);
        var first = new Product("First", category.Id, new Money(1m), new Money(2m), new StockQuantity(1m), new StockQuantity(0m), barcode: new Barcode("FIRST-CODE"));
        var duplicate = new Product("Duplicate", category.Id, new Money(1m), new Money(2m), new StockQuantity(1m), new StockQuantity(0m), barcode: new Barcode("SECOND-CODE"));
        var blank = new Product("Blank", category.Id, new Money(1m), new Money(2m), new StockQuantity(1m), new StockQuantity(0m), barcode: new Barcode("BLANK-CODE"));
        await dbContext.Products.AddRangeAsync([first, duplicate, blank], TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE \"PRODUCTS\" SET \"Barcode\" = 'DUPLICATE-CODE' WHERE \"Id\" IN ({0}, {1})",
            [first.Id.ToString(), duplicate.Id.ToString()]);
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE \"PRODUCTS\" SET \"Barcode\" = '' WHERE \"Id\" = {0}",
            [blank.Id.ToString()]);
        dbContext.ChangeTracker.Clear();

        await migrator.MigrateAsync(null, TestContext.Current.CancellationToken);

        string?[] barcodes = await dbContext.Products
            .AsNoTracking()
            .Select(product => product.Barcode)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, barcodes.Count(barcode => barcode == "DUPLICATE-CODE"));
        Assert.Single(barcodes, barcode => barcode is null); // only blank
        Assert.Single(barcodes, barcode => barcode != null && barcode.StartsWith("INT-", StringComparison.Ordinal)); // duplicate remapped

        int auditCount = await dbContext.AuditLogs
            .CountAsync(log => log.Action == "BarcodeMigration", TestContext.Current.CancellationToken);
        Assert.Equal(1, auditCount);
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

    [Fact]
    public async Task AssignInternalBarcodeAsync_PersistsWithoutChangingStockOrInventory()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        await using var dbContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var category = new Category("Barcodes");
        var product = new Product("Rice", category.Id, new Money(1m), new Money(2m), new StockQuantity(12m), new StockQuantity(2m));
        await dbContext.Categories.AddAsync(category, TestContext.Current.CancellationToken);
        await dbContext.Products.AddAsync(product, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        string barcode = await new AssignInternalBarcodeUseCase(new ProductRepository(dbContext), dbContext)
            .ExecuteAsync(product.Id, TestContext.Current.CancellationToken);
        dbContext.ChangeTracker.Clear();
        Product persisted = await dbContext.Products.SingleAsync(candidate => candidate.Id == product.Id, TestContext.Current.CancellationToken);

        Assert.Equal(barcode, persisted.Barcode);
        Assert.Equal(12m, persisted.CurrentStock);
        Assert.Empty(await dbContext.InventoryMovements.ToListAsync(TestContext.Current.CancellationToken));
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
