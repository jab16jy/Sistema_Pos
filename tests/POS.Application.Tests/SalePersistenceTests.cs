using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs.Invoices;
using POS.Application.DTOs.Receivables;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Queries;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests;

public sealed class SalePersistenceTests
{
    [Fact]
    public async Task ExecuteAsync_WithSqliteRepository_PersistsSalePaymentInvoiceAndStockEffect()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Sugar", new Money(1_000m), new Money(1_400m), new StockQuantity(10m), new StockQuantity(2m), TestContext.Current.CancellationToken);

        var useCase = new CreateSaleUseCase(
            new ProductRepository(dbContext),
            new SaleRepository(dbContext),
            dbContext,
            authorizer: TestAuthorizer.Create(dbContext: dbContext));

        var result = await useCase.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 2m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 2_800m, 3_000m, null)]),
            TestContext.Current.CancellationToken);

        var persistedSale = await dbContext.Sales
            .Include(sale => sale.Items)
            .Include(sale => sale.Payments)
            .SingleAsync(sale => sale.Id == result.SaleId, TestContext.Current.CancellationToken);
        var persistedProduct = await dbContext.Products.SingleAsync(item => item.Id == product.Id, TestContext.Current.CancellationToken);

        Assert.StartsWith("POS-", persistedSale.InvoiceNumber, StringComparison.Ordinal);
        Assert.Equal(2_800m, persistedSale.Total().Amount);
        Payment payment = Assert.Single(persistedSale.Payments);
        Assert.Equal(PaymentMethod.Cash, payment.Method);
        Assert.Equal(2_800m, payment.Amount.Amount);
        Assert.Equal(3_000m, payment.ReceivedAmount?.Amount);
        SaleItem item = Assert.Single(persistedSale.Items);
        Assert.Equal(2m, item.Quantity);
        Assert.Equal(8m, persistedProduct.CurrentStock);
    }

    [Fact]
    public void AddPosInfrastructure_RegistersCreateSaleUseCase()
    {
        var services = new ServiceCollection();

        services.AddPosInfrastructure("Data Source=:memory:");

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        var useCase = scope.ServiceProvider.GetRequiredService<CreateSaleUseCase>();

        Assert.NotNull(useCase);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WhenSalesExist_ReturnsPersistedTotalsAndCounts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Sugar", new Money(1_000m), new Money(1_400m), new StockQuantity(10m), new StockQuantity(2m), TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(new ProductRepository(dbContext), new SaleRepository(dbContext), dbContext, authorizer: TestAuthorizer.Create(dbContext: dbContext));
        await saleUseCase.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 2m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 2_800m, 3_000m, null)]),
            TestContext.Current.CancellationToken);
        await saleUseCase.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Transfer, 1_400m, null, "Nequi")]),
            TestContext.Current.CancellationToken);

        Guid heldSaleCashierId = await TestCashierFactory.CreateAndPersistAsync(dbContext, TestContext.Current.CancellationToken);
        var heldSale = new Sale(heldSaleCashierId);
        heldSale.Hold();
        await dbContext.Sales.AddAsync(heldSale, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var summaryUseCase = new GetDashboardSummaryUseCase(new SaleRepository(dbContext));

        DashboardSummary summary = await summaryUseCase.ExecuteAsync(
            new DashboardSummaryRequest(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, summary.SalesCount);
        Assert.Equal(4_200m, summary.SalesTotal);
        Assert.Equal(2_800m, summary.CashInDrawer);
        Assert.Equal(1_400m, summary.DigitalPayments);
        Assert.Equal(0, summary.LowStockCount);
    }

    [Fact]
    public async Task GetShiftSalesSummaryAsync_IncludesOnlyCompletedSalesFromTheOpenShiftCashier()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Sugar", new Money(1_000m), new Money(1_400m), new StockQuantity(10m), new StockQuantity(2m), TestContext.Current.CancellationToken);
        Guid cashierId = Guid.NewGuid();
        var sales = new SaleRepository(dbContext);
        var useCase = new CreateSaleUseCase(new ProductRepository(dbContext), sales, dbContext, authorizer: TestAuthorizer.Create(cashierId, dbContext));

        await useCase.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 2m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 2_800m, 2_800m, null)]),
            TestContext.Current.CancellationToken);
        await useCase.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Transfer, 1_400m, null, "Nequi")]),
            TestContext.Current.CancellationToken);
        var otherUseCase = new CreateSaleUseCase(new ProductRepository(dbContext), sales, dbContext, authorizer: TestAuthorizer.Create(Guid.NewGuid(), dbContext));
        await otherUseCase.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 1_400m, 1_400m, null)]),
            TestContext.Current.CancellationToken);

        var heldSale = new Sale(cashierId);
        heldSale.Hold();
        await dbContext.Sales.AddAsync(heldSale, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        ShiftSalesSummaryData summary = await sales.GetShiftSalesSummaryAsync(
            cashierId,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            TestContext.Current.CancellationToken);

        Assert.Equal(4_200m, summary.SalesTotal);
        Assert.Equal(2_800m, summary.CashPayments);
        Assert.Equal(1_400m, summary.DigitalPayments);
    }

    [Fact]
    public async Task GetShiftSalesSummaryAsync_ClassifiesPersistedReturnOutflowsWithoutInferringLegacyReturns()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(
            dbContext, "Sugar", new Money(1_000m), new Money(1_400m),
            new StockQuantity(10m), new StockQuantity(2m), TestContext.Current.CancellationToken);
        Guid cashierId = Guid.NewGuid();
        var sales = new SaleRepository(dbContext);
        var useCase = new CreateSaleUseCase(new ProductRepository(dbContext), sales, dbContext, authorizer: TestAuthorizer.Create(cashierId, dbContext));

        CreateSaleResult cashSale = await useCase.ExecuteAsync(new CreateSaleRequest(
            null, [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 1_400m, 1_400m, null)]),
            TestContext.Current.CancellationToken);
        CreateSaleResult digitalSale = await useCase.ExecuteAsync(new CreateSaleRequest(
            null, [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Transfer, 1_400m, null, "Nequi")]),
            TestContext.Current.CancellationToken);

        Guid returnProcessorId = await TestCashierFactory.CreateAndPersistAsync(dbContext, TestContext.Current.CancellationToken);
        var cashReturn = new SaleReturn(cashSale.SaleId, returnProcessorId, "Damaged");
        var digitalReturn = new SaleReturn(digitalSale.SaleId, returnProcessorId, "Damaged");
        var legacyReturn = new SaleReturn(cashSale.SaleId, returnProcessorId, "Legacy");
        await dbContext.SaleReturns.AddRangeAsync([cashReturn, digitalReturn, legacyReturn], TestContext.Current.CancellationToken);
        await dbContext.ReturnSettlements.AddRangeAsync(
            new ReturnSettlement(cashReturn.Id, new Money(400m), PaymentMethod.Cash),
            new ReturnSettlement(digitalReturn.Id, new Money(500m), PaymentMethod.Transfer));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        ShiftSalesSummaryData summary = await sales.GetShiftSalesSummaryAsync(
            cashierId,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            TestContext.Current.CancellationToken);

        Assert.Equal(400m, summary.CashReturnOutflows);
        Assert.Equal(500m, summary.DigitalReturnOutflows);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_ExcludesLoyaltyTenderFromDigitalPaymentsAndIgnoresZeroMinimumStock()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Sugar", new Money(1_000m), new Money(2_800m), new StockQuantity(10m), new StockQuantity(2m), TestContext.Current.CancellationToken);
        Product noThresholdProduct = await TestProductFactory.CreateAndPersistAsync(dbContext, "No threshold", new Money(1_000m), new Money(1_400m), new StockQuantity(0m), new StockQuantity(0m), TestContext.Current.CancellationToken);
        Product lowStockProduct = await TestProductFactory.CreateAndPersistAsync(dbContext, "Low stock", new Money(1_000m), new Money(1_400m), new StockQuantity(1m), new StockQuantity(2m), TestContext.Current.CancellationToken);

        Guid saleCashierId = await TestCashierFactory.CreateAndPersistAsync(dbContext, TestContext.Current.CancellationToken);
        var sale = new Sale(saleCashierId);
        sale.AddItem(product, new Quantity(1m), Money.Zero);
        sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(2_000m), new Money(2_000m), null));
        sale.AddPayment(new Payment(PaymentMethod.LoyaltyPoints, new Money(800m), null, null));
        sale.Complete();
        await dbContext.Sales.AddAsync(sale, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var summaryUseCase = new GetDashboardSummaryUseCase(new SaleRepository(dbContext));

        DashboardSummary summary = await summaryUseCase.ExecuteAsync(
            new DashboardSummaryRequest(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.SalesCount);
        Assert.Equal(2_800m, summary.SalesTotal);
        Assert.Equal(2_000m, summary.CashInDrawer);
        Assert.Equal(0m, summary.DigitalPayments);
        Assert.Equal(1, summary.LowStockCount);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WhenRequestedForToday_ExcludesPreviousDaySalesButKeepsCurrentLowStock()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Sugar", new Money(1_000m), new Money(1_400m), new StockQuantity(10m), new StockQuantity(2m), TestContext.Current.CancellationToken);
        Product lowStockProduct = await TestProductFactory.CreateAndPersistAsync(dbContext, "Low stock", new Money(1_000m), new Money(1_400m), new StockQuantity(1m), new StockQuantity(2m), TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(new ProductRepository(dbContext), new SaleRepository(dbContext), dbContext, authorizer: TestAuthorizer.Create(dbContext: dbContext));
        await saleUseCase.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 1_400m, 1_400m, null)]),
            TestContext.Current.CancellationToken);
        CreateSaleResult yesterdaySale = await saleUseCase.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Transfer, 1_400m, null, "Nequi")]),
            TestContext.Current.CancellationToken);

        Sale persistedYesterdaySale = await dbContext.Sales.SingleAsync(sale => sale.Id == yesterdaySale.SaleId, TestContext.Current.CancellationToken);
        dbContext.Entry(persistedYesterdaySale).Property(nameof(Sale.CreatedAt)).CurrentValue = DateTimeOffset.Now.AddDays(-1);
        dbContext.Entry(persistedYesterdaySale).Property(nameof(Sale.CreatedAt)).IsModified = true;
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset startOfToday = new(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var summaryUseCase = new GetDashboardSummaryUseCase(new SaleRepository(dbContext));

        DashboardSummary summary = await summaryUseCase.ExecuteAsync(
            new DashboardSummaryRequest(startOfToday, startOfToday.AddDays(1)),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, summary.SalesCount);
        Assert.Equal(1_400m, summary.SalesTotal);
        Assert.Equal(1_400m, summary.CashInDrawer);
        Assert.Equal(0m, summary.DigitalPayments);
        Assert.Equal(1, summary.LowStockCount);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_WhenNoSalesExist_ReturnsZeroState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var summaryUseCase = new GetDashboardSummaryUseCase(new SaleRepository(dbContext));

        DashboardSummary summary = await summaryUseCase.ExecuteAsync(
            new DashboardSummaryRequest(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, summary.SalesCount);
        Assert.Equal(0m, summary.SalesTotal);
        Assert.Equal(0m, summary.CashInDrawer);
        Assert.Equal(0m, summary.DigitalPayments);
        Assert.Equal(0, summary.LowStockCount);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_UsesExclusiveUtcUpperBound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(
            dbContext, "Boundary product", new Money(1_000m), new Money(1_400m),
            new StockQuantity(10m), new StockQuantity(2m), TestContext.Current.CancellationToken);
        var saleUseCase = new CreateSaleUseCase(new ProductRepository(dbContext), new SaleRepository(dbContext), dbContext, authorizer: TestAuthorizer.Create(dbContext: dbContext));
        CreateSaleResult result = await saleUseCase.ExecuteAsync(new CreateSaleRequest(
            null, [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 1_400m, 1_400m, null)]),
            TestContext.Current.CancellationToken);

        DateTimeOffset upperBound = DateTimeOffset.UtcNow.AddMinutes(1);
        Sale persistedSale = await dbContext.Sales.SingleAsync(sale => sale.Id == result.SaleId, TestContext.Current.CancellationToken);
        dbContext.Entry(persistedSale).Property(nameof(Sale.CreatedAt)).CurrentValue = upperBound;
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DashboardSummary summary = await new GetDashboardSummaryUseCase(new SaleRepository(dbContext)).ExecuteAsync(
            new DashboardSummaryRequest(upperBound.AddDays(-1), upperBound),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, summary.SalesCount);
        Assert.Equal(0m, summary.SalesTotal);
    }

    // --- RED: deferred balance / receivable persistence ---

    [Fact]
    public async Task ExecuteAsync_WithDeferredBalance_PersistsReceivableLinkedToSaleAndCustomer()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Monitor", new Money(200_000m), new Money(350_000m), new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);

        var customer = new Customer("Deferred Customer", "def-cust-" + Guid.NewGuid().ToString("N")[..8]);
        await dbContext.Customers.AddAsync(customer, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var customerId = customer.Id;

        var useCase = new CreateSaleUseCase(
            new ProductRepository(dbContext),
            new SaleRepository(dbContext),
            dbContext,
            new ReceivableRepository(dbContext),
            authorizer: TestAuthorizer.Create(dbContext: dbContext));

        var request = new CreateSaleRequest(
            customerId,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            new SaleSettlementPlan(200_000m, PaymentMethod.Transfer, 150_000m, "Nequi"));

        var result = await useCase.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(150_000m, result.DeferredBalance);

        var receivable = await dbContext.Receivables
            .SingleAsync(r => r.SaleId == result.SaleId, TestContext.Current.CancellationToken);
        Assert.Equal(customerId, receivable.CustomerId);
        Assert.Equal(150_000m, receivable.OutstandingBalance.Amount);

        Payment payment = await dbContext.Set<Payment>()
            .SingleAsync(p => p.SaleId == result.SaleId, TestContext.Current.CancellationToken);
        Assert.Equal(PaymentMethod.Transfer, payment.Method);
        Assert.Equal("Nequi", payment.BankOrPlatform);

        DashboardSummary summary = await new GetDashboardSummaryUseCase(new SaleRepository(dbContext)).ExecuteAsync(
            new DashboardSummaryRequest(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)),
            TestContext.Current.CancellationToken);

        Assert.Equal(350_000m, summary.SalesTotal);
        Assert.Equal(0m, summary.CashInDrawer);
        Assert.Equal(200_000m, summary.DigitalPayments);
    }

    [Fact]
    public async Task ReceivableLookupQueryService_WithOpenReceivable_ReturnsItWithoutTranslationError()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Monitor", new Money(200_000m), new Money(350_000m), new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);

        var customer = new Customer("Receivable Customer", "rec-cust-" + Guid.NewGuid().ToString("N")[..8]);
        await dbContext.Customers.AddAsync(customer, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var customerId = customer.Id;
        var useCase = new CreateSaleUseCase(
            new ProductRepository(dbContext),
            new SaleRepository(dbContext),
            dbContext,
            new ReceivableRepository(dbContext),
            authorizer: TestAuthorizer.Create(dbContext: dbContext));
        var result = await useCase.ExecuteAsync(new CreateSaleRequest(
            customerId,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            new SaleSettlementPlan(200_000m, PaymentMethod.Cash, 150_000m)),
            TestContext.Current.CancellationToken);

        var lookup = new ReceivableLookupQueryService(dbContext);

        // Before the fix this threw: SQLite could not translate .Where(r => r.OutstandingBalance.Amount > 0)
        // because OutstandingBalance is a converted Money value object.
        IReadOnlyList<ReceivableSummary> open = await lookup.GetOpenReceivablesAsync(
            customerId, 1, 100, TestContext.Current.CancellationToken);

        ReceivableSummary summary = Assert.Single(open);
        Assert.Equal(150_000m, summary.OutstandingBalance);
        Assert.Equal(result.InvoiceNumber, summary.InvoiceNumber);
    }

    [Fact]
    public async Task SalesHistoryQueryService_AfterCompletedSale_ReturnsItWithCorrectTotal()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Sugar", new Money(1_000m), new Money(1_400m), new StockQuantity(10m), new StockQuantity(2m), TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(new ProductRepository(dbContext), new SaleRepository(dbContext), dbContext, authorizer: TestAuthorizer.Create(dbContext: dbContext));
        await saleUseCase.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 2m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 2_800m, 3_000m, null)]),
            TestContext.Current.CancellationToken);

        var historyService = new SalesHistoryQueryService(dbContext);

        IReadOnlyList<SaleHistoryResult> results = await historyService.SearchAsync(
            new SalesHistoryRequest(null, null, null),
            TestContext.Current.CancellationToken);

        SaleHistoryResult result = Assert.Single(results);
        Assert.Equal(2_800m, result.Total);
    }

    [Fact]
    public async Task SalesHistoryQueryService_GetDetailAsync_ReturnsItemsPaymentsAndComputedChange()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Sugar", new Money(1_000m), new Money(1_400m), new StockQuantity(10m), new StockQuantity(2m), TestContext.Current.CancellationToken);

        var saleUseCase = new CreateSaleUseCase(new ProductRepository(dbContext), new SaleRepository(dbContext), dbContext, authorizer: TestAuthorizer.Create(dbContext: dbContext));
        CreateSaleResult created = await saleUseCase.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 2m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 2_800m, 3_000m, null)]),
            TestContext.Current.CancellationToken);

        var historyService = new SalesHistoryQueryService(dbContext);

        InvoiceDetailResult? detail = await historyService.GetDetailAsync(created.SaleId, TestContext.Current.CancellationToken);

        Assert.NotNull(detail);
        Assert.Equal(created.InvoiceNumber, detail.InvoiceNumber);
        InvoiceDetailItemResult item = Assert.Single(detail.Items);
        Assert.Equal("Sugar", item.ProductName);
        Assert.Equal(2_800m, item.Subtotal);
        InvoicePaymentResult payment = Assert.Single(detail.Payments);
        Assert.Equal(PaymentMethod.Cash, payment.Method);
        Assert.Equal(2_800m, payment.Amount);
        Assert.Equal(3_000m, payment.ReceivedAmount);
        Assert.Equal(200m, payment.ChangeAmount);
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
