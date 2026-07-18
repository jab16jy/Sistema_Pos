using POS.Application.DTOs.Invoices;
using POS.Application.Interfaces;
using POS.Application.Services;
using POS.Application.UseCases.Invoices;
using POS.Application.UseCases.Loyalty;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests.Invoices;

public sealed class ProcessReturnUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_DerivesPartialAndFullRefundsFromSaleSnapshots()
    {
        var partial = CreateScenario();
        partial.Returns.Seed(CreateReturn(partial.Sale, partial.SaleItemId, 1, 9m));

        await partial.UseCase.ExecuteAsync(new ProcessReturnRequest(partial.Sale.Id, "Partial", Guid.NewGuid(), ReturnResolutionChoice.Cash, [new(partial.SaleItemId, 2)]));

        Assert.Equal(18m, Assert.Single(partial.Returns.Events.Last().Items).RefundAmount.Amount);

        var full = CreateScenario();
        await full.UseCase.ExecuteAsync(new ProcessReturnRequest(full.Sale.Id, "Full", Guid.NewGuid(), ReturnResolutionChoice.Cash));

        Assert.Equal(36m, Assert.Single(full.Returns.Events).TotalRefund().Amount);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsQuantitiesBeyondCumulativeReturnLimitWithoutMutating()
    {
        var scenario = CreateScenario();
        scenario.Returns.Seed(CreateReturn(scenario.Sale, scenario.SaleItemId, 3, 27m));

        await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.UseCase.ExecuteAsync(
            new ProcessReturnRequest(scenario.Sale.Id, "Excess", Guid.NewGuid(), ReturnResolutionChoice.Cash, [new(scenario.SaleItemId, 2)])));

        AssertRejected(scenario, 6m, expectedReturns: 1);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsNonCompletedSaleWithoutMutating()
    {
        var scenario = CreateScenario(complete: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.UseCase.ExecuteAsync(
            new ProcessReturnRequest(scenario.Sale.Id, "Draft", Guid.NewGuid(), ReturnResolutionChoice.Cash)));

        AssertRejected(scenario, 6m, expectedReturns: 0, expectedStatus: SaleStatus.Draft);
    }

    [Fact]
    public async Task ExecuteAsync_RepeatedOperationIdReturnsPersistedReturnWithoutDuplicating()
    {
        var scenario = CreateScenario();
        var operationId = Guid.NewGuid();
        var request = new ProcessReturnRequest(scenario.Sale.Id, "Retry", operationId, ReturnResolutionChoice.Cash, [new(scenario.SaleItemId, 1)]);

        Guid first = await scenario.UseCase.ExecuteAsync(request);
        Guid second = await scenario.UseCase.ExecuteAsync(request);

        Assert.Equal(first, second);
        Assert.Single(scenario.Returns.Events);
        Assert.Single(scenario.Inventory.Movements);
        Assert.Equal(1, scenario.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_TwoPartialReturnsCompensateTheSameAsAFullReturn()
    {
        var scenario = CreateScenario();
        LoyaltyTransaction earned = new(Guid.NewGuid(), LoyaltyTransactionType.Earned, 90, scenario.Sale.Id);
        LoyaltyTransaction redeemed = new(Guid.NewGuid(), LoyaltyTransactionType.Redeemed, -10, scenario.Sale.Id);
        scenario.Loyalty.Rows.AddRange([earned, redeemed]);

        await scenario.UseCase.ExecuteAsync(new ProcessReturnRequest(
            scenario.Sale.Id, "First", Guid.NewGuid(), ReturnResolutionChoice.Cash, [new(scenario.SaleItemId, 2)]), TestContext.Current.CancellationToken);
        Guid secondOperationId = Guid.NewGuid();
        await scenario.UseCase.ExecuteAsync(new ProcessReturnRequest(
            scenario.Sale.Id, "Second", secondOperationId, ReturnResolutionChoice.Cash, [new(scenario.SaleItemId, 2)]), TestContext.Current.CancellationToken);
        await scenario.UseCase.ExecuteAsync(new ProcessReturnRequest(
            scenario.Sale.Id, "Second", secondOperationId, ReturnResolutionChoice.Cash, [new(scenario.SaleItemId, 2)]), TestContext.Current.CancellationToken);

        LoyaltyTransaction[] compensations = scenario.Loyalty.Rows.Where(row => row.CompensatesTransactionId is not null).ToArray();
        Assert.Equal(-90, compensations.Where(row => row.CompensatesTransactionId == earned.Id).Sum(row => row.Points));
        Assert.Equal(10, compensations.Where(row => row.CompensatesTransactionId == redeemed.Id).Sum(row => row.Points));
        Assert.Equal(4, compensations.Length);
        Assert.All(compensations, row => Assert.True(row.SaleReturnId is Guid returnId && scenario.Returns.Events.Any(@return => @return.Id == returnId)));
    }

    [Theory]
    [InlineData(ReturnResolutionChoice.Cash, PaymentMethod.Cash)]
    [InlineData(ReturnResolutionChoice.DebitCard, PaymentMethod.DebitCard)]
    public async Task ExecuteAsync_StagesExactlyOneCashOrDigitalSettlement(ReturnResolutionChoice choice, PaymentMethod paymentMethod)
    {
        var scenario = CreateScenario();

        await scenario.UseCase.ExecuteAsync(new ProcessReturnRequest(
            scenario.Sale.Id, "Resolved", Guid.NewGuid(), choice, [new(scenario.SaleItemId, 1)]));

        ReturnSettlement settlement = Assert.Single(scenario.Receivables.ReturnSettlements);
        Assert.Equal(9m, settlement.Amount.Amount);
        Assert.Equal(paymentMethod, settlement.PaymentMethod);
        Assert.Empty(scenario.Receivables.Reconciliations);
    }

    [Fact]
    public async Task ExecuteAsync_ReconcilesMatchingReceivableWithoutCreatingSettlement()
    {
        var scenario = CreateScenario(receivableBalance: 20m);

        await scenario.UseCase.ExecuteAsync(new ProcessReturnRequest(
            scenario.Sale.Id, "Credit", Guid.NewGuid(), ReturnResolutionChoice.Receivable, [new(scenario.SaleItemId, 1)]));

        Assert.Equal(11m, scenario.Receivables.Source!.OutstandingBalance.Amount);
        ReturnReceivableReconciliation reconciliation = Assert.Single(scenario.Receivables.Reconciliations);
        Assert.Equal(scenario.Receivables.Source.Id, reconciliation.ReceivableId);
        Assert.Equal(9m, reconciliation.Amount.Amount);
        Assert.Empty(scenario.Receivables.ReturnSettlements);
    }

    [Fact]
    public async Task ExecuteAsync_RejectsMismatchedOrInsufficientReceivableWithoutMutating()
    {
        var scenario = CreateScenario(receivableBalance: 20m, matchReceivable: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => scenario.UseCase.ExecuteAsync(new ProcessReturnRequest(
            scenario.Sale.Id, "Invalid credit", Guid.NewGuid(), ReturnResolutionChoice.Receivable, [new(scenario.SaleItemId, 1)])));

        AssertRejected(scenario, 6m, expectedReturns: 0);
        Assert.Equal(20m, scenario.Receivables.Source!.OutstandingBalance.Amount);
        Assert.Empty(scenario.Receivables.Reconciliations);
        Assert.Empty(scenario.Receivables.ReturnSettlements);

        var insufficient = CreateScenario(receivableBalance: 5m);
        await Assert.ThrowsAsync<InvalidOperationException>(() => insufficient.UseCase.ExecuteAsync(new ProcessReturnRequest(
            insufficient.Sale.Id, "Insufficient credit", Guid.NewGuid(), ReturnResolutionChoice.Receivable, [new(insufficient.SaleItemId, 1)])));

        AssertRejected(insufficient, 6m, expectedReturns: 0);
        Assert.Equal(5m, insufficient.Receivables.Source!.OutstandingBalance.Amount);
    }

    [Fact]
    public void ReturnContract_ContainsNoCallerSuppliedRefundAmount()
    {
        Assert.Equal(2, typeof(ReturnLineRequest).GetConstructors().Single().GetParameters().Length);
        Assert.DoesNotContain(typeof(ProcessReturnRequest).GetProperties(), property => property.Name.Contains("Refund", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_UsesCanonicalActorForReturnAndInventory()
    {
        var scenario = CreateScenario();

        await scenario.UseCase.ExecuteAsync(new ProcessReturnRequest(
            scenario.Sale.Id, "Authorized", Guid.NewGuid(), ReturnResolutionChoice.Cash, [new(scenario.SaleItemId, 1)]));

        Assert.Equal(scenario.Actor.Id, Assert.Single(scenario.Returns.Events).ProcessedByUserId);
        Assert.Equal(scenario.Actor.Id, Assert.Single(scenario.Inventory.Movements).UserId);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task ExecuteAsync_RejectsUnauthenticatedInactiveOrUnauthorizedActorBeforeMutation(
        bool authenticated,
        bool active,
        bool permitted)
    {
        var scenario = CreateScenario(authenticated: authenticated, active: active, permitted: permitted);

        await Assert.ThrowsAsync<UnauthorizedActionException>(() => scenario.UseCase.ExecuteAsync(
            new ProcessReturnRequest(scenario.Sale.Id, "Rejected", Guid.NewGuid(), ReturnResolutionChoice.Cash, [new(scenario.SaleItemId, 1)])));

        AssertRejected(scenario, 6m, expectedReturns: 0);
    }

    private static void AssertRejected(Scenario scenario, decimal stock, int expectedReturns, SaleStatus expectedStatus = SaleStatus.Completed)
    {
        Assert.Equal(expectedStatus, scenario.Sale.Status);
        Assert.Equal(stock, scenario.Product.CurrentStock);
        Assert.Empty(scenario.Inventory.Movements);
        Assert.Equal(expectedReturns, scenario.Returns.Events.Count);
        Assert.Equal(0, scenario.UnitOfWork.SaveCount);
    }

    private static Scenario CreateScenario(
        bool complete = true,
        decimal? receivableBalance = null,
        bool matchReceivable = true,
        bool authenticated = true,
        bool active = true,
        bool permitted = true)
    {
        var product = new Product("Product", Guid.NewGuid(), new Money(5m), new Money(10m), new StockQuantity(10m), new StockQuantity(0m));
        Guid customerId = Guid.NewGuid();
        var sale = new Sale(Guid.NewGuid(), customerId);
        sale.AddItem(product, new Quantity(4m), new Money(4m));
        if (complete)
        {
            sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(36m), new Money(36m)));
            sale.Complete();
        }

        var returns = new ReturnRepository();
        var inventory = new Inventory();
        var unitOfWork = new UnitOfWork();
        var loyalty = new Loyalty();
        var actor = new User("return-actor", "hash", Guid.NewGuid());
        if (!active)
        {
            actor.Deactivate();
        }
        var receivables = new Receivables(receivableBalance is null
            ? null
            : new Receivable(matchReceivable ? customerId : Guid.NewGuid(), matchReceivable ? sale.Id : Guid.NewGuid(), new Money(receivableBalance.Value)));
        return new Scenario(sale, product, sale.Items.Single().Id, returns, inventory, unitOfWork, receivables, loyalty, actor,
            new ProcessReturnUseCase(
                new Sales(sale), new Products(product), inventory, returns, unitOfWork, new Users(actor), new CompensateSaleLoyaltyUseCase(loyalty), receivables,
                new CriticalCommandAuthorizer(new Session(actor.Id, authenticated), new Users(actor), new Roles(permitted))));
    }

    private static SaleReturn CreateReturn(Sale sale, Guid saleItemId, int quantity, decimal refund)
    {
        var saleReturn = new SaleReturn(sale.Id, sale.CashierUserId, "Earlier", Guid.NewGuid());
        SaleItem item = sale.Items.Single();
        saleReturn.AddItem(new SaleReturnItem(saleReturn.Id, saleItemId, item.ProductId, item.ProductName, quantity, item.UnitPrice, new Money(refund)));
        return saleReturn;
    }

    private sealed record Scenario(Sale Sale, Product Product, Guid SaleItemId, ReturnRepository Returns, Inventory Inventory, UnitOfWork UnitOfWork, Receivables Receivables, Loyalty Loyalty, User Actor, ProcessReturnUseCase UseCase);
    private sealed class Sales(Sale sale) : ISaleRepository
    {
        public Task AddAsync(Sale value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Sale?>(id == sale.Id ? sale : null);
        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class Products(Product product) : IProductRepository
    {
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(id == product.Id ? product : null);
        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task AddAsync(Product value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ReturnRepository : ISaleReturnRepository
    {
        public List<SaleReturn> Events { get; } = [];
        public void Seed(SaleReturn saleReturn) => Events.Add(saleReturn);
        public Task AddAsync(SaleReturn saleReturn, CancellationToken cancellationToken = default) { Events.Add(saleReturn); return Task.CompletedTask; }
        public Task<SaleReturn?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.FromResult<SaleReturn?>(Events.SingleOrDefault(value => value.OriginalSaleId == saleId));
        public Task<IReadOnlyList<SaleReturn>> GetAllBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SaleReturn>>(Events.Where(value => value.OriginalSaleId == saleId).ToList());
        public Task<SaleReturn?> GetByOperationIdAsync(Guid operationId, CancellationToken cancellationToken = default) => Task.FromResult<SaleReturn?>(Events.SingleOrDefault(value => value.OperationId == operationId));
    }

    private sealed class Inventory : IInventoryRepository
    {
        public List<InventoryMovement> Movements { get; } = [];
        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryStockLevel>>([]);
        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryMovement>>([]);
        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]);
        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default) { Movements.Add(movement); return Task.CompletedTask; }
    }

    private sealed class UnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(++SaveCount);
    }

    private sealed class Receivables(Receivable? source) : IReceivableRepository
    {
        public Receivable? Source { get; } = source;
        public List<ReturnSettlement> ReturnSettlements { get; } = [];
        public List<ReturnReceivableReconciliation> Reconciliations { get; } = [];
        public Task AddAsync(Receivable receivable, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Receivable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Source?.Id == id ? Source : null);
        public Task<Receivable?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.FromResult(Source?.SaleId == saleId ? Source : null);
        public Task UpdateAsync(Receivable receivable, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddReturnSettlementAsync(ReturnSettlement settlement, CancellationToken cancellationToken = default) { ReturnSettlements.Add(settlement); return Task.CompletedTask; }
        public Task AddReturnReceivableReconciliationAsync(ReturnReceivableReconciliation reconciliation, CancellationToken cancellationToken = default) { Reconciliations.Add(reconciliation); return Task.CompletedTask; }
    }

    private sealed class Loyalty : ILoyaltyRepository
    {
        public List<LoyaltyTransaction> Rows { get; } = [];
        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default) { Rows.Add(transaction); return Task.CompletedTask; }
        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(Rows.Where(row => row.SaleId == saleId).ToList());
        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<LoyaltyAccount?>(null);
        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);
        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken cancellationToken = default) => Task.FromResult<LoyaltyRule?>(null);
        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LoyaltyRule>>([]);
        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> GetTotalIssuedPointsAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);
    }

    private sealed class Session(Guid actorId, bool authenticated) : ICurrentOperatorSession
    {
        public Guid OperatorId => actorId;
        public string Username => "return-actor";
        public bool IsAuthenticated => authenticated;
    }

    private sealed class Roles(bool permitted) : IRoleRepository
    {
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Role?>(new Role("Cashier"));
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Role?>(null);
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>([]);
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(permitted && permissionKey == PermissionKeys.Invoices.Return);
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task AddAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class Users(User? user = null) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(user?.Id == id ? user : null);
        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>([]);
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }
}
