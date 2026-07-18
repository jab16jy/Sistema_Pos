using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Reporting;
using POS.Application.DTOs.Sales;
using POS.Application.Interfaces;
using POS.Application.Services;
using POS.Application.UseCases.Reporting;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Authorization;
using POS.Domain.Interfaces;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
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
            new ReceivableRepository(dbContext),
            authorizer: TestAuthorizer.Create(dbContext: dbContext));

        // Cash sale: 50_000
        await createSale.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 50_000m, 50_000m, null)]),
            TestContext.Current.CancellationToken);

        // Digital sale: 30_000 (transfer)
        Product product2 = await TestProductFactory.CreateAndPersistAsync(dbContext, "Gadget", new Money(10_000m), new Money(30_000m), new StockQuantity(20m), new StockQuantity(2m), TestContext.Current.CancellationToken);

        await createSale.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product2.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Transfer, 30_000m, null, "Nequi")]),
            TestContext.Current.CancellationToken);

        // Receivable settlements: 20_000 cash + 10_000 digital via RECEIVABLE_SETTLEMENTS
        var receivableCustomer = new Customer("Receivable Customer", "rec-cust-" + Guid.NewGuid().ToString("N")[..8]);
        var receivableSale = new Sale(await TestCashierFactory.CreateAndPersistAsync(dbContext, TestContext.Current.CancellationToken), receivableCustomer.Id);
        await dbContext.Customers.AddAsync(receivableCustomer, TestContext.Current.CancellationToken);
        await dbContext.Sales.AddAsync(receivableSale, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var receivable = new Receivable(receivableCustomer.Id, receivableSale.Id, new Money(40_000m));
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
        Assert.Equal(0m, result.Report.CashReturnOutflows);
        Assert.Equal(0m, result.Report.DigitalReturnOutflows);
        Assert.Equal(0m, result.Report.ExpectedDrawerCash);
    }

    [Fact]
    public async Task GetOperationalReport_AttributesBilledSalesAndCashReturnOutflowsToTheirOwnDates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Returned cash item", new Money(10_000m), new Money(50_000m), new StockQuantity(5m), new StockQuantity(1m), TestContext.Current.CancellationToken);
        var createSale = new CreateSaleUseCase(new ProductRepository(dbContext), new SaleRepository(dbContext), dbContext, authorizer: TestAuthorizer.Create(dbContext: dbContext));
        CreateSaleResult createdSale = await createSale.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 50_000m, 50_000m, null)]),
            TestContext.Current.CancellationToken);

        Sale sale = await dbContext.Sales.SingleAsync(s => s.Id == createdSale.SaleId, TestContext.Current.CancellationToken);
        sale.MarkReturned();
        Guid returnProcessorId = await TestCashierFactory.CreateAndPersistAsync(dbContext, TestContext.Current.CancellationToken);
        var saleReturn = new SaleReturn(sale.Id, returnProcessorId, "Customer return");
        var settlement = new ReturnSettlement(saleReturn.Id, new Money(50_000m), PaymentMethod.Cash);
        await dbContext.SaleReturns.AddAsync(saleReturn, TestContext.Current.CancellationToken);
        await dbContext.ReturnSettlements.AddAsync(settlement, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE SALES SET CreatedAt = {"2026-07-01 12:00:00.0000000+00:00"} WHERE Id = {sale.Id.ToString()}", TestContext.Current.CancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE RETURN_SETTLEMENTS SET SettledAt = {"2026-07-03 12:00:00.0000000+00:00"} WHERE Id = {settlement.Id.ToString()}", TestContext.Current.CancellationToken);

        var queryService = new OperationalReportQueryService(dbContext);
        OperationalReportDto salesDay = await queryService.GetTotalsAsync(new ReportRangeRequest(
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 1, 23, 59, 59, TimeSpan.Zero)), TestContext.Current.CancellationToken);
        OperationalReportDto returnDay = await queryService.GetTotalsAsync(new ReportRangeRequest(
            new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 3, 23, 59, 59, TimeSpan.Zero)), TestContext.Current.CancellationToken);

        Assert.Equal(50_000m, salesDay.BilledSalesTotal);
        Assert.Equal(0m, salesDay.CashReturnOutflows);
        Assert.Equal(0m, returnDay.BilledSalesTotal);
        Assert.Equal(50_000m, returnDay.CashReturnOutflows);
    }

    [Fact]
    public async Task GetOperationalReport_ClassifiesDigitalOutflowsAndIgnoresSettlementLessReturns()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Return item", new Money(10_000m), new Money(20_000m), new StockQuantity(10m), new StockQuantity(1m), TestContext.Current.CancellationToken);
        var createSale = new CreateSaleUseCase(new ProductRepository(dbContext), new SaleRepository(dbContext), dbContext, authorizer: TestAuthorizer.Create(dbContext: dbContext));
        CreateSaleResult digitalSale = await createSale.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Transfer, 20_000m, null, "Nequi")]),
            TestContext.Current.CancellationToken);
        Product legacyProduct = await TestProductFactory.CreateAndPersistAsync(dbContext, "Legacy return item", new Money(10_000m), new Money(10_000m), new StockQuantity(10m), new StockQuantity(1m), TestContext.Current.CancellationToken);
        CreateSaleResult legacySale = await createSale.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(legacyProduct.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 10_000m, 10_000m, null)]),
            TestContext.Current.CancellationToken);

        Sale persistedDigitalSale = await dbContext.Sales.SingleAsync(s => s.Id == digitalSale.SaleId, TestContext.Current.CancellationToken);
        Sale persistedLegacySale = await dbContext.Sales.SingleAsync(s => s.Id == legacySale.SaleId, TestContext.Current.CancellationToken);
        persistedDigitalSale.MarkReturned();
        persistedLegacySale.MarkReturned();
        Guid returnProcessorId = await TestCashierFactory.CreateAndPersistAsync(dbContext, TestContext.Current.CancellationToken);
        var digitalReturn = new SaleReturn(persistedDigitalSale.Id, returnProcessorId, "Digital return");
        var legacyReturn = new SaleReturn(persistedLegacySale.Id, returnProcessorId, "Legacy return");
        var digitalSettlement = new ReturnSettlement(digitalReturn.Id, new Money(20_000m), PaymentMethod.Transfer);
        await dbContext.SaleReturns.AddRangeAsync([digitalReturn, legacyReturn], TestContext.Current.CancellationToken);
        await dbContext.ReturnSettlements.AddAsync(digitalSettlement, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE RETURN_SETTLEMENTS SET SettledAt = {"2026-07-03 12:00:00.0000000+00:00"} WHERE Id = {digitalSettlement.Id.ToString()}", TestContext.Current.CancellationToken);

        var queryService = new OperationalReportQueryService(dbContext);
        OperationalReportDto report = await queryService.GetTotalsAsync(new ReportRangeRequest(
            new DateTimeOffset(2026, 7, 3, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 3, 23, 59, 59, TimeSpan.Zero)), TestContext.Current.CancellationToken);

        Assert.Equal(0m, report.CashReturnOutflows);
        Assert.Equal(20_000m, report.DigitalReturnOutflows);
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
        var useCase = new CloseCashRegisterUseCase(queryService, cashClosingRepo, dbContext, CreateAuthorizer());

        // CountedCash is null — should be rejected
        var request = new CashClosingRequest(
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
            dbContext,
            authorizer: TestAuthorizer.Create(dbContext: dbContext));

        await createSale.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 50_000m, 60_000m, null)]),
            TestContext.Current.CancellationToken);

        var queryService = new OperationalReportQueryService(dbContext);
        var cashClosingRepo = new CashClosingRepository(dbContext);
        var useCase = new CloseCashRegisterUseCase(queryService, cashClosingRepo, dbContext, CreateAuthorizer());

        // Counted cash is 48_000 — operator short by 2_000
        var request = new CashClosingRequest(
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
            dbContext,
            authorizer: TestAuthorizer.Create(dbContext: dbContext));

        await createSale.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(cashProduct.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 40_000m, 40_000m, null)]),
            TestContext.Current.CancellationToken);

        await createSale.ExecuteAsync(new CreateSaleRequest(
            null,
            [new CreateSaleItemRequest(digitalProduct.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Transfer, 20_000m, null, "Nequi")]),
            TestContext.Current.CancellationToken);

        var queryService = new OperationalReportQueryService(dbContext);
        var cashClosingRepo = new CashClosingRepository(dbContext);
        var useCase = new CloseCashRegisterUseCase(queryService, cashClosingRepo, dbContext, CreateAuthorizer());

        var result = await useCase.ExecuteAsync(new CashClosingRequest(
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

    // ─────────────────────────────────────────────────────
    // CloseCashRegisterUseCase — authorization & canonical actor
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task CloseCashRegister_AuthorizedClosing_UsesSessionActorAndPersistsClosing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var user = new User("cashier1", "hash", RoleConstants.AdministratorRoleId);
        var authorizer = new CriticalCommandAuthorizer(
            new TestSession(user.Id),
            new TestUsers(user),
            new TestRoles());

        var queryService = new OperationalReportQueryService(dbContext);
        var cashClosingRepo = new CashClosingRepository(dbContext);
        var useCase = new CloseCashRegisterUseCase(queryService, cashClosingRepo, dbContext, authorizer);

        var request = new CashClosingRequest(
            From: DateTimeOffset.UtcNow.AddDays(-1),
            To: DateTimeOffset.UtcNow.AddDays(1),
            CountedCash: 0m);

        var result = await useCase.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.Error);
        var closing = await dbContext.CashClosings.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(user.Id, closing.UserId);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task CloseCashRegister_RejectsInvalidAuthorizationBeforeQueryingOrPersistingClosing(
        bool authenticated,
        bool active,
        bool permitted)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var roleId = Guid.NewGuid();
        var user = new User("cashier1", "hash", roleId);
        if (!active)
        {
            user.Deactivate();
        }

        var authorizer = new CriticalCommandAuthorizer(
            new TestSession(user.Id, authenticated),
            new TestUsers(user),
            new TestRoles(permitted, roleId));

        var queryService = new OperationalReportQueryService(dbContext);
        var cashClosingRepo = new CashClosingRepository(dbContext);
        var useCase = new CloseCashRegisterUseCase(queryService, cashClosingRepo, dbContext, authorizer);

        var request = new CashClosingRequest(
            From: DateTimeOffset.UtcNow.AddDays(-1),
            To: DateTimeOffset.UtcNow.AddDays(1),
            CountedCash: 100m);

        await Assert.ThrowsAsync<UnauthorizedActionException>(() =>
            useCase.ExecuteAsync(request, TestContext.Current.CancellationToken));

        Assert.Equal(0, await dbContext.CashClosings.CountAsync(TestContext.Current.CancellationToken));
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        return new AppDbContext(options);
    }

    private static CriticalCommandAuthorizer CreateAuthorizer()
    {
        var user = new User("cashier", "hash", RoleConstants.AdministratorRoleId);
        return new CriticalCommandAuthorizer(new TestSession(user.Id), new TestUsers(user), new TestRoles());
    }

    private sealed class TestSession(Guid operatorId, bool isAuthenticated = true) : ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = operatorId;
        public string Username => "cashier";
        public bool IsAuthenticated { get; } = isAuthenticated;
    }

    private sealed class TestUsers(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<User?>(id == user.Id ? user : null);
        public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) => Task.FromResult<User?>(null);
        public Task<bool> AnyAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task AddAsync(User value, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>([user]);
        public Task UpdateAsync(User value, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class TestRoles(bool hasPermission = true, Guid? targetRoleId = null) : IRoleRepository
    {
        private static readonly Role Administrator = Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName);
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            if (targetRoleId.HasValue && id == targetRoleId.Value)
            {
                return Task.FromResult<Role?>(new Role("Cashier"));
            }
            return Task.FromResult<Role?>(id == Administrator.Id ? Administrator : null);
        }
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Role?>(null);
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>([Administrator]);
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(hasPermission);
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task AddAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }
}
