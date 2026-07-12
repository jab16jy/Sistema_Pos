using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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
        await using var connection = await OpenAsync();
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
        await using var connection = await OpenAsync();
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
    public async Task SaveSupplierCatalogItemAsync_CreatesUpdatesAndDeactivatesWithoutDuplicates()
    {
        await using var connection = await OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var supplier = new Supplier("Acme Foods", "900123");
        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Rice", new Money(1_000m), new Money(1_500m), new StockQuantity(5m), new StockQuantity(2m), TestContext.Current.CancellationToken);
        await dbContext.Suppliers.AddAsync(supplier, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var save = new SaveSupplierCatalogItemUseCase(new SupplierRepository(dbContext), new ProductRepository(dbContext), new ProductSupplierRepository(dbContext), dbContext);

        await save.ExecuteAsync(new SaveSupplierCatalogItemRequest(supplier.Id, product.Id, "RICE-12", "Bulto", 12m, 12_000m, true), TestContext.Current.CancellationToken);
        await save.ExecuteAsync(new SaveSupplierCatalogItemRequest(supplier.Id, product.Id, "RICE-24", "Bulto", 24m, 24_000m, false), TestContext.Current.CancellationToken);

        ProductSupplier item = Assert.Single(await dbContext.ProductSuppliers.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal("RICE-24", item.SupplierSku);
        Assert.Equal(24m, item.UnitsPerPurchaseUnit);
        Assert.False(item.IsActive);
    }

    [Fact]
    public void ProductSupplier_WithInvalidMetadata_RejectsConfiguration()
    {
        Assert.Throws<ArgumentException>(() => new ProductSupplier(Guid.NewGuid(), Guid.NewGuid(), null, "", 1m, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProductSupplier(Guid.NewGuid(), Guid.NewGuid(), null, "Unidad", 0m, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProductSupplier(Guid.NewGuid(), Guid.NewGuid(), null, "Unidad", 1m, 0m));
    }

    [Fact]
    public void SupplierCatalogItemResult_DisplayLabel_UsesDashSeparatedPurchaseIdentity()
    {
        var item = new SupplierCatalogItemResult(
            Guid.NewGuid(), "Acme Foods", Guid.NewGuid(), "Rice", "RICE-REF", "RICE-12", "Bulto", 12m, 12_000m, true);

        Assert.Contains("Rice — RICE-12 — Bulto —", item.DisplayLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("·", item.DisplayLabel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterPurchase_UsesActiveCatalogSnapshotsAndWeightedAverageCost()
    {
        await using var connection = await OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        (Supplier supplier, Product product) = await SeedSupplierProductAndCatalogAsync(db, 5m, 1_000m, 12m, 10_000m);

        RegisterPurchaseResult result = await CreateRegisterUseCase(db).ExecuteAsync(new RegisterPurchaseRequest(supplier.Id, Guid.NewGuid(), new DateOnly(2026, 7, 11), "FAC-1", "30 days", "Transfer", "first delivery", 19m, [new RegisterPurchaseItemRequest(product.Id, 2m)]), TestContext.Current.CancellationToken);

        Purchase purchase = await db.Purchases.Include(p => p.Items).SingleAsync(TestContext.Current.CancellationToken);
        PurchaseItem line = Assert.Single(purchase.Items);
        Product persisted = await db.Products.SingleAsync(TestContext.Current.CancellationToken);
        InventoryMovement movement = await db.InventoryMovements.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(result.PurchaseId, purchase.Id);
        Assert.Equal(24m, line.InventoryUnitsReceived);
        Assert.Equal(20_000m, line.Subtotal?.Amount);
        Assert.Equal(833.33m, line.BaseUnitCost?.Amount);
        Assert.Equal(29m, persisted.CurrentStock);
        Assert.Equal(862.07m, persisted.CostPrice?.Amount);
        Assert.Equal(24m, movement.Quantity);
        Assert.Equal(23_800m, purchase.Total?.Amount);
    }

    [Fact]
    public async Task RegisterPurchase_RejectsMissingOrInactiveCatalogWithoutPersistingPartialReceipt()
    {
        await using var connection = await OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var supplier = new Supplier("Acme", "900123");
        Product product = await TestProductFactory.CreateAndPersistAsync(db, "Rice", new Money(100m), new Money(200m), new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);
        db.Suppliers.Add(supplier); await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateRegisterUseCase(db).ExecuteAsync(NewRequest(supplier.Id, product.Id), TestContext.Current.CancellationToken));

        Assert.Equal(5m, (await db.Products.SingleAsync(TestContext.Current.CancellationToken)).CurrentStock);
        Assert.Empty(await db.Purchases.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.InventoryMovements.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RegisterPurchaseAsync_WithInactiveOrIncompleteCatalog_RejectsBeforeStockChange()
    {
        await using var connection = await OpenAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var supplier = new Supplier("Acme Foods", "900123");
        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Rice", new Money(1_000m), new Money(1_500m), new StockQuantity(5m), new StockQuantity(2m), TestContext.Current.CancellationToken);
        await dbContext.Suppliers.AddAsync(supplier, TestContext.Current.CancellationToken);
        await dbContext.ProductSuppliers.AddAsync(new ProductSupplier(product.Id, supplier.Id), TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var useCase = CreateRegisterUseCase(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new RegisterPurchaseRequest(
            supplier.Id,
            Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), null, null, "Contado", null, 0m,
            [new RegisterPurchaseItemRequest(product.Id, 7m)]),
            TestContext.Current.CancellationToken));

        Product persisted = await dbContext.Products.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5m, persisted.CurrentStock);
        Assert.Empty(await dbContext.Purchases.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await dbContext.InventoryMovements.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RegisterPurchase_WhenCommitFails_LeavesPurchaseMovementStockAndCostUnchanged()
    {
        await using var connection = await OpenAsync();
        await using var setup = CreateDbContext(connection);
        await setup.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        (Supplier supplier, Product product) = await SeedSupplierProductAndCatalogAsync(setup, 5m, 100m, 12m, 120m);
        await using var failing = CreateDbContext(connection);
        var useCase = new RegisterPurchaseUseCase(new SupplierRepository(failing), new ProductRepository(failing), new ProductSupplierRepository(failing), new PurchaseRepository(failing), new ThrowingUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(NewRequest(supplier.Id, product.Id), TestContext.Current.CancellationToken));

        await using var verification = CreateDbContext(connection);
        Product persisted = await verification.Products.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5m, persisted.CurrentStock);
        Assert.Equal(100m, persisted.CostPrice?.Amount);
        Assert.Empty(await verification.Purchases.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await verification.InventoryMovements.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PurchaseHistory_FiltersInclusiveDatesInvoiceAndPaginatesPersistedHeaders()
    {
        await using var connection = await OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        (Supplier supplier, Product product) = await SeedSupplierProductAndCatalogAsync(db, 0m, null, 1m, 100m);
        RegisterPurchaseUseCase register = CreateRegisterUseCase(db);
        await register.ExecuteAsync(new RegisterPurchaseRequest(supplier.Id, Guid.NewGuid(), new DateOnly(2026, 7, 1), "A-1", null, "Cash", null, 0m, [new RegisterPurchaseItemRequest(product.Id, 1m)]), TestContext.Current.CancellationToken);
        await register.ExecuteAsync(new RegisterPurchaseRequest(supplier.Id, Guid.NewGuid(), new DateOnly(2026, 7, 2), "A-2", null, "Cash", null, 0m, [new RegisterPurchaseItemRequest(product.Id, 1m)]), TestContext.Current.CancellationToken);

        PagedResult<PurchaseHistoryResult> page = await new SearchPurchaseHistoryUseCase(new PurchaseHistoryQueryService(db)).ExecuteAsync(new PurchaseHistoryRequest(supplier.Id, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 1), "A-1", 1, 1), TestContext.Current.CancellationToken);

        PurchaseHistoryResult header = Assert.Single(page.Items);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal("A-1", header.InvoiceNumber);
        Assert.Equal(100m, header.Total);
        Assert.Equal(1m, Assert.Single(header.Items).InventoryUnitsReceived);
    }

    [Fact]
    public async Task PurchaseHistory_LegacyRowsRemainVisibleWithoutReinterpretingLegacyTotalCost()
    {
        await using var connection = await OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        (Supplier supplier, Product product) = await SeedSupplierProductAndCatalogAsync(db, 0m, null, 1m, 100m);
        Guid purchaseId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO PURCHASES (Id, SupplierId, UserId, CreatedAt) VALUES ({purchaseId.ToString()}, {supplier.Id.ToString()}, {Guid.NewGuid().ToString()}, {"2026-07-11 00:00:00.0000000+00:00"})", TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO PURCHASE_ITEMS (Id, PurchaseId, ProductId, Quantity, total_cost) VALUES ({Guid.NewGuid().ToString()}, {purchaseId.ToString()}, {product.Id.ToString()}, {3m}, {777m})", TestContext.Current.CancellationToken);

        PurchaseHistoryResult history = Assert.Single((await new PurchaseHistoryQueryService(db).SearchAsync(new PurchaseHistoryRequest(supplier.Id, null, null, null), TestContext.Current.CancellationToken)).Items);
        PurchaseHistoryLineResult line = Assert.Single(history.Items);
        Assert.Null(history.Total);
        Assert.Null(line.Subtotal);
        Assert.Equal(777m, line.LegacyTotalCost);
    }

    [Fact]
    public async Task SupplierQuotation_AcceptsAndPreloadsImmutablePurchaseDraftWithoutInventoryMutation()
    {
        await using var connection = await OpenAsync();
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        (Supplier supplier, Product product) = await SeedSupplierProductAndCatalogAsync(db, 5m, 100m, 12m, 120m);
        var catalog = new ProductSupplierRepository(db);
        var quotations = new SupplierQuotationRepository(db);
        var create = new CreateSupplierQuotationUseCase(new SupplierRepository(db), new ProductRepository(db), catalog, quotations, db);
        SupplierQuotationResult quotation = await create.ExecuteAsync(new CreateSupplierQuotationRequest(supplier.Id, new DateOnly(2026, 7, 11), "30 days", null, [new CreateSupplierQuotationItemRequest(product.Id, 2m)]), TestContext.Current.CancellationToken);

        await new AcceptSupplierQuotationUseCase(quotations, db).ExecuteAsync(quotation.Id, TestContext.Current.CancellationToken);
        ProductSupplier catalogItem = (await catalog.GetAsync(supplier.Id, product.Id, TestContext.Current.CancellationToken))!;
        catalogItem.Update("NEW-SKU", "Wholesale", 24m, 400m, true);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        PurchaseDraftFromQuotationResult draft = await new ConvertSupplierQuotationToPurchaseDraftUseCase(quotations).ExecuteAsync(quotation.Id, TestContext.Current.CancellationToken);

        Assert.Equal(5m, (await db.Products.SingleAsync(TestContext.Current.CancellationToken)).CurrentStock);
        PurchaseDraftItem item = Assert.Single(draft.Items);
        Assert.Equal(12m, item.UnitsPerPurchaseUnit);
        Assert.Equal(120m, item.PurchaseUnitCost);
        Assert.Empty(await db.InventoryMovements.ToListAsync(TestContext.Current.CancellationToken));

        await CreateRegisterUseCase(db).ExecuteAsync(new RegisterPurchaseRequest(supplier.Id, Guid.NewGuid(), draft.EffectiveDate, null, draft.PaymentTerms, "Cash", draft.Observations, 0m, draft.Items.Select(i => new RegisterPurchaseItemRequest(i.ProductId, i.Quantity)).ToList(), draft.SupplierQuotationId), TestContext.Current.CancellationToken);
        Purchase confirmed = await db.Purchases.Include(p => p.Items).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(quotation.Id, confirmed.SupplierQuotationId);
        Assert.Equal(12m, confirmed.Items.Single().UnitsPerPurchaseUnit);
        Assert.Equal(120m, confirmed.Items.Single().PurchaseUnitCost?.Amount);
    }

    [Fact]
    public async Task SupplierPurchasingMigration_ConvertsLegacyBinarySupplierIdsToText()
    {
        await using var connection = await OpenAsync();
        await using var legacy = CreateDbContext(connection);
        await legacy.GetService<IMigrator>().MigrateAsync("20260710174227_EnforceProductCategoryReference", TestContext.Current.CancellationToken);
        Guid supplierId = Guid.NewGuid();
        await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Suppliers\" (\"Id\", \"Name\", \"TaxId\", \"IsActive\") VALUES ({supplierId.ToByteArray()}, {"Legacy supplier"}, {"900123"}, {true})", TestContext.Current.CancellationToken);

        await legacy.GetService<IMigrator>().MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

        await using var verification = CreateDbContext(connection);
        Supplier supplier = await verification.Suppliers.SingleAsync(item => item.Id == supplierId, TestContext.Current.CancellationToken);
        Assert.Equal("Legacy supplier", supplier.Name);
    }

    [Fact]
    public void AddPosInfrastructure_ResolvesSupplierPurchasingPortsAndUseCases()
    {
        var services = new ServiceCollection(); services.AddPosInfrastructure("Data Source=:memory:");
        using ServiceProvider provider = services.BuildServiceProvider(); using IServiceScope scope = provider.CreateScope();
        Assert.IsType<ProductSupplierRepository>(scope.ServiceProvider.GetRequiredService<IProductSupplierRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GetSupplierCatalogUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<GetEligibleSupplierCatalogUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SaveSupplierCatalogItemUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SearchPurchaseHistoryUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CreateSupplierQuotationUseCase>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISupplierCatalogQueryService>());
    }

    private static RegisterPurchaseRequest NewRequest(Guid supplierId, Guid productId) => new(supplierId, Guid.NewGuid(), new DateOnly(2026, 7, 11), null, null, "Cash", null, 19m, [new RegisterPurchaseItemRequest(productId, 1m)]);
    private static RegisterPurchaseUseCase CreateRegisterUseCase(AppDbContext db) => new(new SupplierRepository(db), new ProductRepository(db), new ProductSupplierRepository(db), new PurchaseRepository(db), db, new SupplierQuotationRepository(db));
    private static async Task<SqliteConnection> OpenAsync() { var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(TestContext.Current.CancellationToken); return connection; }
    private static AppDbContext CreateDbContext(SqliteConnection connection) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private static async Task<(Supplier Supplier, Product Product)> SeedSupplierProductAndCatalogAsync(AppDbContext db, decimal stock, decimal? cost, decimal factor, decimal purchaseCost)
    {
        var supplier = new Supplier("Acme", "900123");
        Product product = await TestProductFactory.CreateAndPersistAsync(db, "Rice", cost is null ? null : new Money(cost.Value), new Money(200m), new StockQuantity(stock), new StockQuantity(1m), TestContext.Current.CancellationToken);
        db.Suppliers.Add(supplier); db.ProductSuppliers.Add(new ProductSupplier(product.Id, supplier.Id, "R-1", "Dozen", factor, purchaseCost));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken); return (supplier, product);
    }

    private sealed class ThrowingUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated failure.");
    }
}
