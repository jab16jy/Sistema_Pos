using POS.Application.DTOs.Sales;
using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;
using POS.Application.UseCases.Loyalty;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests;

public sealed class CashierHoldDiscountUseCaseTests
{
    [Fact]
    public async Task ManualDiscountAboveConfiguredMaximum_IsRejectedBeforeStockOrPersistence()
    {
        var product = Product();
        var sales = new SaleRepositoryFake();
        var unitOfWork = new UnitOfWorkFake();
        var settings = new SettingsRepositoryFake(Settings(maximumDiscount: 20m));
        var useCase = new CreateSaleUseCase(
            new ProductRepositoryFake(product),
            sales,
            unitOfWork,
            null,
            null,
            null,
            null,
            settings);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new CreateSaleRequest(
                Guid.NewGuid(),
                null,
                [new CreateSaleItemRequest(product.Id, 1m, 0m, ManualDiscountPercent: 25m)],
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 1_000m, 1_000m, null)]),
            TestContext.Current.CancellationToken));

        Assert.Equal(10m, product.CurrentStock);
        Assert.Empty(sales.Sales);
        Assert.Equal(0, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task HoldResumeAndComplete_ReserveStockOnlyOnce()
    {
        var product = Product();
        var sales = new SaleRepositoryFake();
        var unitOfWork = new UnitOfWorkFake();
        var inventory = new InventoryRepositoryFake();
        var settings = new SettingsRepositoryFake(Settings(maximumDiscount: 20m));
        Guid cashierId = Guid.NewGuid();

        var hold = new HoldSaleUseCase(new ProductRepositoryFake(product), sales, unitOfWork, inventory, settings);
        HeldSaleSummary held = await hold.ExecuteAsync(
            new HoldSaleRequest(
                cashierId,
                null,
                [new CreateSaleItemRequest(product.Id, 1m, 0m)]),
            TestContext.Current.CancellationToken);

        Assert.Equal(9m, product.CurrentStock);
        Assert.Single(inventory.Movements);

        var resume = new ResumeHeldSaleUseCase(sales, unitOfWork);
        await resume.ExecuteAsync(new ResumeHeldSaleRequest(cashierId, held.SaleId), TestContext.Current.CancellationToken);
        Assert.Equal(SaleStatus.Draft, sales.Sales.Single().Status);
        Assert.Equal(9m, product.CurrentStock);

        var complete = new CompleteHeldSaleUseCase(sales, unitOfWork);
        await complete.ExecuteAsync(
            new CompleteHeldSaleRequest(
                cashierId,
                held.SaleId,
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 3_000m, 3_000m, null)]),
            TestContext.Current.CancellationToken);

        Assert.Equal(SaleStatus.Completed, sales.Sales.Single().Status);
        Assert.Equal(9m, product.CurrentStock);
        Assert.Single(inventory.Movements);
    }

    [Fact]
    public async Task CompletingHeldCustomerSale_StagesLoyaltyPoints()
    {
        var product = Product();
        var customer = new Customer("Ana Gomez", "123");
        var sales = new SaleRepositoryFake();
        var unitOfWork = new UnitOfWorkFake();
        var loyalty = new LoyaltyRepositoryFake(new LoyaltyRule(0.001m, 10m, 100));
        Guid cashierId = Guid.NewGuid();
        var sale = new Sale(cashierId, customer.Id);
        sale.AddItem(product, new Quantity(1m), Money.Zero);
        sale.Hold();
        sale.Resume();
        sales.Sales.Add(sale);

        var complete = new CompleteHeldSaleUseCase(
            sales,
            unitOfWork,
            new AccumulateLoyaltyPointsUseCase(loyalty, unitOfWork));

        await complete.ExecuteAsync(
            new CompleteHeldSaleRequest(
                cashierId,
                sale.Id,
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 3_000m, 3_000m, null)]),
            TestContext.Current.CancellationToken);

        LoyaltyTransaction transaction = Assert.Single(loyalty.Transactions);
        Assert.Equal(LoyaltyTransactionType.Earned, transaction.Type);
        Assert.Equal(sale.Id, transaction.SaleId);
        Assert.Equal(3, transaction.Points);
    }

    [Fact]
    public async Task SettingsMaximum_IsMappedThroughSaveUseCase()
    {
        var settings = new SettingsRepositoryFake(null);
        var unitOfWork = new UnitOfWorkFake();
        var session = new SessionFake(POS.Domain.Authorization.PermissionKeys.Settings.Edit);
        var useCase = new POS.Application.UseCases.Settings.SaveSettingsUseCase(settings, unitOfWork, session);

        SettingsResult result = await useCase.ExecuteAsync(
            new POS.Application.DTOs.Settings.SaveSettingsRequest(
                "Store", null, null, null, null, null, null, null, false, null, null, null, null, null, "COP", 35m),
            TestContext.Current.CancellationToken);

        Assert.Equal(35m, result.MaximumCashierDiscountPercent);
        Assert.Equal(35m, settings.Stored!.MaximumCashierDiscountPercent);
    }

    private static Product Product() => new(
        "Coffee", Guid.NewGuid(), new Money(2_000m), new Money(3_000m), new StockQuantity(10m), new StockQuantity(1m));

    private static BusinessSettings Settings(decimal maximumDiscount) => new(
        "Store", null, null, null, null, null, null, null, false, null, null, null, null, null, "COP", maximumDiscount);

    private sealed class ProductRepositoryFake(Product product) : IProductRepository
    {
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(id == product.Id ? product : null);
        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default) => Task.FromResult<Product?>(null);
        public Task AddAsync(Product value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SaleRepositoryFake : ISaleRepository
    {
        public List<Sale> Sales { get; } = [];
        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default) { Sales.Add(sale); return Task.CompletedTask; }
        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Sales.SingleOrDefault(sale => sale.Id == id));
        public Task<IReadOnlyList<Sale>> GetOnHoldByCashierAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Sale>>(Sales.Where(sale => sale.CashierUserId == cashierUserId && sale.Status == SaleStatus.OnHold).ToArray());
        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class InventoryRepositoryFake : IInventoryRepository
    {
        public List<InventoryMovement> Movements { get; } = [];
        public Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default) { Movements.Add(movement); return Task.CompletedTask; }
        public Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryStockLevel>>([]);
        public Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InventoryMovement>>(Movements);
        public Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Product>>([]);
    }

    private sealed class SettingsRepositoryFake(BusinessSettings? settings) : ISettingsRepository
    {
        public BusinessSettings? Stored { get; private set; } = settings;
        public Task<BusinessSettings?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Stored);
        public Task SaveAsync(BusinessSettings value, CancellationToken cancellationToken = default) { Stored = value; return Task.CompletedTask; }
    }

    private sealed class UnitOfWorkFake : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.FromResult(1); }
    }

    private sealed class LoyaltyRepositoryFake(LoyaltyRule rule) : ILoyaltyRepository
    {
        public LoyaltyAccount? Account { get; private set; }
        public List<LoyaltyTransaction> Transactions { get; } = [];

        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(Account?.CustomerId == customerId ? Account : null);

        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken cancellationToken = default)
        {
            Account = account;
            return Task.CompletedTask;
        }

        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default)
        {
            Transactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(Transactions.Where(transaction => transaction.LoyaltyAccountId == loyaltyAccountId).Take(maxCount).ToList());

        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken cancellationToken = default) => Task.FromResult<LoyaltyRule?>(rule);
        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LoyaltyRule>>([rule]);
        public Task AddRuleAsync(LoyaltyRule value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateRuleAsync(LoyaltyRule value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken cancellationToken = default)
            => Task.FromResult(Transactions.Where(transaction => transaction.LoyaltyAccountId == loyaltyAccountId).Sum(transaction => transaction.Points));
        public Task<int> GetTotalIssuedPointsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Transactions.Where(transaction => transaction.Type == LoyaltyTransactionType.Earned).Sum(transaction => transaction.Points));
        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Account is null ? 0 : 1);
        public Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(Transactions.Take(maxCount).ToList());
    }

    private sealed class SessionFake(string permission) : ICurrentOperatorSession
    {
        public Guid OperatorId => Guid.NewGuid();
        public string Username => "admin";
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string> { permission };
        public bool IsAuthenticated => true;
    }
}
