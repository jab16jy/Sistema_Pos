using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;
using POS.Application.UseCases.Purchases;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Queries;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests;

public sealed class SupplierPurchaseUseCaseTests
{
    [Fact]
    public async Task SaveSupplierAsync_WithValidSupplier_PersistsSearchableSupplier()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var save = new SaveSupplierUseCase(new SupplierRepository(dbContext), dbContext);
        var search = new SearchSuppliersUseCase(new SupplierLookupQueryService(dbContext));

        SupplierResult result = await save.ExecuteAsync(new SaveSupplierRequest(null, "Acme Foods", "900123", "+57 300 111 2233", "sales@acme.test", true), TestContext.Current.CancellationToken);
        IReadOnlyList<SupplierSearchResult> suppliers = await search.ExecuteAsync(new SupplierSearchRequest("Acme", IncludeInactive: false), TestContext.Current.CancellationToken);

        SupplierSearchResult match = Assert.Single(suppliers);
        Assert.Equal(result.Id, match.Id);
        Assert.Equal("Acme Foods", match.Name);
        Assert.Equal("900123", match.TaxId);
    }

    [Fact]
    public async Task SaveSupplierAsync_WithExistingSupplier_UpdatesThenDeactivatesRecord()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var save = new SaveSupplierUseCase(new SupplierRepository(dbContext), dbContext);
        var search = new SearchSuppliersUseCase(new SupplierLookupQueryService(dbContext));
        SupplierResult created = await save.ExecuteAsync(new SaveSupplierRequest(null, "Acme Foods", "900123", null, null, true), TestContext.Current.CancellationToken);

        SupplierResult updated = await save.ExecuteAsync(new SaveSupplierRequest(created.Id, "Acme Wholesale", "900456", "+57 301 222 3344", null, false), TestContext.Current.CancellationToken);
        IReadOnlyList<SupplierSearchResult> activeOnly = await search.ExecuteAsync(new SupplierSearchRequest("Acme", IncludeInactive: false), TestContext.Current.CancellationToken);
        IReadOnlyList<SupplierSearchResult> all = await search.ExecuteAsync(new SupplierSearchRequest("Acme", IncludeInactive: true), TestContext.Current.CancellationToken);

        Assert.Equal(created.Id, updated.Id);
        Assert.False(updated.IsActive);
        Assert.Empty(activeOnly);
        SupplierSearchResult inactive = Assert.Single(all);
        Assert.Equal("Acme Wholesale", inactive.Name);
    }

    [Fact]
    public async Task RegisterPurchaseAsync_WithKnownSupplier_IncreasesStockAndCreatesMovementTrace()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var supplier = new Supplier("Acme Foods", "900123");
        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Rice", new Money(1_000m), new Money(1_500m), new StockQuantity(5m), new StockQuantity(2m), TestContext.Current.CancellationToken);
        await dbContext.Suppliers.AddAsync(supplier, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var useCase = CreateRegisterPurchaseUseCase(dbContext);

        RegisterPurchaseResult result = await useCase.ExecuteAsync(new RegisterPurchaseRequest(
            supplier.Id,
            Guid.NewGuid(),
            [new RegisterPurchaseItemRequest(product.Id, 7m, 7_000m)]),
            TestContext.Current.CancellationToken);

        Product persisted = await dbContext.Products.SingleAsync(TestContext.Current.CancellationToken);
        InventoryMovement movement = await dbContext.InventoryMovements.SingleAsync(TestContext.Current.CancellationToken);
        Purchase purchase = await dbContext.Purchases.Include(p => p.Items).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(result.PurchaseId, purchase.Id);
        Assert.Equal(12m, persisted.CurrentStock);
        Assert.Equal(product.Id, movement.ProductId);
        Assert.Equal(7m, movement.Quantity);
        Assert.Equal(purchase.Id, movement.ReferenceId);
        Assert.Equal(7_000m, purchase.Items.Single().TotalCost.Amount);
    }

    [Fact]
    public async Task RegisterPurchaseAsync_WithUnknownSupplier_RejectsWithoutStockChange()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Rice", new Money(1_000m), new Money(1_500m), new StockQuantity(5m), new StockQuantity(2m), TestContext.Current.CancellationToken);
        var useCase = CreateRegisterPurchaseUseCase(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new RegisterPurchaseRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new RegisterPurchaseItemRequest(product.Id, 7m, 7_000m)]),
            TestContext.Current.CancellationToken));

        Product persisted = await dbContext.Products.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5m, persisted.CurrentStock);
        Assert.Empty(await dbContext.Purchases.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await dbContext.InventoryMovements.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RegisterPurchaseAsync_WithEmptyUserId_RejectsBeforeStockChange()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var supplier = new Supplier("Acme Foods", "900123");
        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Rice", new Money(1_000m), new Money(1_500m), new StockQuantity(5m), new StockQuantity(2m), TestContext.Current.CancellationToken);
        await dbContext.Suppliers.AddAsync(supplier, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var useCase = CreateRegisterPurchaseUseCase(dbContext);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(new RegisterPurchaseRequest(
            supplier.Id,
            Guid.Empty,
            [new RegisterPurchaseItemRequest(product.Id, 7m, 7_000m)]),
            TestContext.Current.CancellationToken));

        Product persisted = await dbContext.Products.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5m, persisted.CurrentStock);
        Assert.Empty(await dbContext.Purchases.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await dbContext.InventoryMovements.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RegisterPurchaseAsync_WhenCommitFails_LeavesNoPersistedPurchaseStockOrMovement()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var setupContext = CreateDbContext(connection);
        await setupContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var supplier = new Supplier("Acme Foods", "900123");
        Product product = await TestProductFactory.CreateAndPersistAsync(setupContext, "Rice", new Money(1_000m), new Money(1_500m), new StockQuantity(5m), new StockQuantity(2m), TestContext.Current.CancellationToken);
        await setupContext.Suppliers.AddAsync(supplier, TestContext.Current.CancellationToken);
        await setupContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var failingContext = CreateDbContext(connection);
        var useCase = new RegisterPurchaseUseCase(
            new SupplierRepository(failingContext),
            new ProductRepository(failingContext),
            new PurchaseRepository(failingContext),
            new ThrowingUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new RegisterPurchaseRequest(
            supplier.Id,
            Guid.NewGuid(),
            [new RegisterPurchaseItemRequest(product.Id, 7m, 7_000m)]),
            TestContext.Current.CancellationToken));

        await using var verificationContext = CreateDbContext(connection);
        Product persisted = await verificationContext.Products.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5m, persisted.CurrentStock);
        Assert.Empty(await verificationContext.Purchases.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await verificationContext.InventoryMovements.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void AddPosInfrastructure_RegistersSupplierPurchaseContracts()
    {
        var services = new ServiceCollection();

        services.AddPosInfrastructure("Data Source=:memory:");

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SaveSupplierUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SearchSuppliersUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RegisterPurchaseUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISupplierRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPurchaseRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISupplierLookupQueryService>());
    }

    private static RegisterPurchaseUseCase CreateRegisterPurchaseUseCase(AppDbContext dbContext)
        => new(new SupplierRepository(dbContext), new ProductRepository(dbContext), new PurchaseRepository(dbContext), dbContext);

    private sealed class ThrowingUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated commit failure.");
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
