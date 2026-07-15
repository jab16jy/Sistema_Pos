using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Reporting;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Reporting;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using POS.Infrastructure.Persistence.Queries;
using Xunit;

namespace POS.Application.Tests;

/// <summary>
/// RED tests for WU4 reporting use cases.
/// GetOperationalReportUseCase: separates billed, cash, digital, and settlement totals.
/// CloseCashRegisterUseCase: rejects missing counted cash, computes correct difference.
/// Invalid report date range is rejected.
/// </summary>
public sealed class ReportingUseCaseTests
{
    // ─────────────────────────────────────────────────────
    // GetOperationalReportUseCase — date range validation
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetOperationalReport_WithInvalidRange_ReturnsFailure()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var queryService = new OperationalReportQueryService(dbContext);
        var useCase = new GetOperationalReportUseCase(queryService);

        var request = new ReportRangeRequest(
            From: DateTimeOffset.UtcNow.AddDays(1),   // 'From' is after 'To' — invalid
            To: DateTimeOffset.UtcNow.AddDays(-1));

        var result = await useCase.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    // ─────────────────────────────────────────────────────
    // GetOperationalReportUseCase — correct total separation
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetOperationalReport_WithMixedTenders_SeparatesBilledCashDigitalAndSettlements()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Arrange: seed one cash sale (50_000), one digital sale (30_000), and one receivable settlement (20_000 cash)
        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Widget", new Money(10_000m), new Money(50_000m), new StockQuantity(20m), new StockQuantity(2m), TestContext.Current.CancellationToken);

        var createSale = new CreateSaleUseCase(
            new ProductRepository(dbContext),
            new SaleRepository(dbContext),
            dbContext,
            new ReceivableRepository(dbContext));

        // Cash sale: 50_000
        await createSale.ExecuteAsync(new CreateSaleRequest(
            Guid.NewGuid(),
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 50_000m, 50_000m, null)]),
            TestContext.Current.CancellationToken);

        // Digital sale: 30_000 (transfer)
        Product product2 = await TestProductFactory.CreateAndPersistAsync(dbContext, "Gadget", new Money(10_000m), new Money(30_000m), new StockQuantity(20m), new StockQuantity(2m), TestContext.Current.CancellationToken);

        await createSale.ExecuteAsync(new CreateSaleRequest(
            Guid.NewGuid(),
            null,
            [new CreateSaleItemRequest(product2.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Transfer, 30_000m, null, "Nequi")]),
            TestContext.Current.CancellationToken);

        // Receivable settlements: 20_000 cash + 10_000 digital via RECEIVABLE_SETTLEMENTS
        var customerId = Guid.NewGuid();
        var receivable = new Receivable(customerId, Guid.NewGuid(), new Money(40_000m));
        receivable.ApplyPayment(new Money(20_000m), PaymentMethod.Cash);
        receivable.ApplyPayment(new Money(10_000m), PaymentMethod.Transfer, "Nequi");
        await dbContext.Receivables.AddAsync(receivable, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var queryService = new OperationalReportQueryService(dbContext);
        var useCase = new GetOperationalReportUseCase(queryService);

        var request = new ReportRangeRequest(
            From: DateTimeOffset.UtcNow.AddDays(-1),
            To: DateTimeOffset.UtcNow.AddDays(1));

        // Act
        var result = await useCase.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsSuccess, result.Error);
        var report = result.Report!;
        Assert.Equal(80_000m, report.BilledSalesTotal);           // 50k + 30k
        Assert.Equal(50_000m, report.SaleCashCollected);          // cash sale only
        Assert.Equal(30_000m, report.SaleDigitalCollected);       // digital sale only
        Assert.Equal(30_000m, report.ReceivableSettlementTotal);  // settlements paid
        Assert.Equal(20_000m, report.ReceivableSettlementCash);
        Assert.Equal(10_000m, report.ReceivableSettlementDigital);
        // Expected drawer cash includes only cash settlements.
        Assert.Equal(70_000m, report.ExpectedDrawerCash);         // 50k + 20k
    }

    [Fact]
    public async Task GetOperationalReport_WithNoActivity_ReturnsAllZeros()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var queryService = new OperationalReportQueryService(dbContext);
        var useCase = new GetOperationalReportUseCase(queryService);

        var request = new ReportRangeRequest(
            From: DateTimeOffset.UtcNow.AddDays(-1),
            To: DateTimeOffset.UtcNow.AddDays(1));

        var result = await useCase.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(0m, result.Report!.BilledSalesTotal);
        Assert.Equal(0m, result.Report.SaleCashCollected);
        Assert.Equal(0m, result.Report.SaleDigitalCollected);
        Assert.Equal(0m, result.Report.ReceivableSettlementTotal);
        Assert.Equal(0m, result.Report.ExpectedDrawerCash);
    }

    // ─────────────────────────────────────────────────────
    // CloseCashRegisterUseCase — missing counted cash
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task CloseCashRegister_WithoutCountedCash_ReturnsFailure()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var queryService = new OperationalReportQueryService(dbContext);
        var cashClosingRepo = new CashClosingRepository(dbContext);
        var useCase = new CloseCashRegisterUseCase(queryService, cashClosingRepo, dbContext);

        // CountedCash is null — should be rejected
        var request = new CashClosingRequest(
            OperatorId: Guid.NewGuid(),
            From: DateTimeOffset.UtcNow.AddDays(-1),
            To: DateTimeOffset.UtcNow.AddDays(1),
            CountedCash: null);

        var result = await useCase.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        // W2: open state must be preserved — no CashClosing row written on rejection
        Assert.Equal(0, await dbContext.CashClosings.CountAsync(TestContext.Current.CancellationToken));
    }

    // ─────────────────────────────────────────────────────
    // CloseCashRegisterUseCase — correct difference computation
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task CloseCashRegister_WithCountedCash_ComputesCorrectDifference()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        // Seed a cash sale of 50_000
        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Item", new Money(10_000m), new Money(50_000m), new StockQuantity(10m), new StockQuantity(1m), TestContext.Current.CancellationToken);

        var createSale = new CreateSaleUseCase(
            new ProductRepository(dbContext),
            new SaleRepository(dbContext),
            dbContext);

        await createSale.ExecuteAsync(new CreateSaleRequest(
            Guid.NewGuid(),
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 50_000m, 60_000m, null)]),
            TestContext.Current.CancellationToken);

        var queryService = new OperationalReportQueryService(dbContext);
        var cashClosingRepo = new CashClosingRepository(dbContext);
        var useCase = new CloseCashRegisterUseCase(queryService, cashClosingRepo, dbContext);

        // Counted cash is 48_000 — operator short by 2_000
        var request = new CashClosingRequest(
            OperatorId: Guid.NewGuid(),
            From: DateTimeOffset.UtcNow.AddDays(-1),
            To: DateTimeOffset.UtcNow.AddDays(1),
            CountedCash: 48_000m);

        var result = await useCase.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(50_000m, result.Summary!.BilledTotal);
        Assert.Equal(50_000m, result.Summary.CashCollected);
        Assert.Equal(0m, result.Summary.DigitalCollected);
        Assert.Equal(50_000m, result.Summary.ExpectedDrawerCash);
        Assert.Equal(48_000m, result.Summary.CountedCash);
        Assert.Equal(-2_000m, result.Summary.Difference);           // 48k - 50k
    }

    // ─────────────────────────────────────────────────────
    // CloseCashRegisterUseCase — mixed-tender digital separation (W1)
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task CloseCashRegister_WithMixedTenders_SeparatesDigitalFromCash()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product cashProduct = await TestProductFactory.CreateAndPersistAsync(dbContext, "Cash Item", new Money(5_000m), new Money(40_000m), new StockQuantity(10m), new StockQuantity(1m), TestContext.Current.CancellationToken);
        Product digitalProduct = await TestProductFactory.CreateAndPersistAsync(dbContext, "Digital Item", new Money(5_000m), new Money(20_000m), new StockQuantity(10m), new StockQuantity(1m), TestContext.Current.CancellationToken);

        var createSale = new CreateSaleUseCase(
            new ProductRepository(dbContext),
            new SaleRepository(dbContext),
            dbContext);

        await createSale.ExecuteAsync(new CreateSaleRequest(
            Guid.NewGuid(), null,
            [new CreateSaleItemRequest(cashProduct.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 40_000m, 40_000m, null)]),
            TestContext.Current.CancellationToken);

        await createSale.ExecuteAsync(new CreateSaleRequest(
            Guid.NewGuid(), null,
            [new CreateSaleItemRequest(digitalProduct.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Transfer, 20_000m, null, "Nequi")]),
            TestContext.Current.CancellationToken);

        var queryService = new OperationalReportQueryService(dbContext);
        var cashClosingRepo = new CashClosingRepository(dbContext);
        var useCase = new CloseCashRegisterUseCase(queryService, cashClosingRepo, dbContext);

        var result = await useCase.ExecuteAsync(new CashClosingRequest(
            OperatorId: Guid.NewGuid(),
            From: DateTimeOffset.UtcNow.AddDays(-1),
            To: DateTimeOffset.UtcNow.AddDays(1),
            CountedCash: 40_000m),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(60_000m, result.Summary!.BilledTotal);       // 40k + 20k
        Assert.Equal(40_000m, result.Summary.CashCollected);      // cash sale only
        Assert.Equal(20_000m, result.Summary.DigitalCollected);   // digital sale only
        Assert.Equal(40_000m, result.Summary.ExpectedDrawerCash); // only cash in drawer
        Assert.Equal(0m, result.Summary.Difference);              // 40k counted == 40k expected
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }
}
