using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Loyalty;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests;

// RED tests for settlement plan and receivable creation (WU3)
public sealed class CreateSaleUseCaseSettlementTests
{
    [Fact]
    public async Task ExecuteAsync_WithDeferredBalance_CreatesReceivableAndReturnsCollectedCash()
    {
        var product = new Product("Laptop", Guid.NewGuid(), new Money(500_000m), new Money(800_000m), new StockQuantity(5m), new StockQuantity(1m));
        var products = new InMemProductRepo(product);
        var sales = new InMemSaleRepo();
        var receivables = new InMemReceivableRepo();
        var unitOfWork = new InMemUoW();
        var customerId = Guid.NewGuid();

        var useCase = new CreateSaleUseCase(products, sales, unitOfWork, receivables);

        var request = new CreateSaleRequest(
            Guid.NewGuid(),
            customerId,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            new SaleSettlementPlan(400_000m, PaymentMethod.Cash, 400_000m));

        var result = await useCase.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.SaleId);
        Assert.Equal(400_000m, result.CashInDrawer);    // only collected cash
        Assert.Equal(400_000m, result.DeferredBalance); // receivable portion
        Assert.Single(receivables.Stored);
        Assert.Equal(customerId, receivables.Stored[0].CustomerId);
        Assert.Equal(400_000m, receivables.Stored[0].OutstandingBalance.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_WithDeferredBalance_WhenNoCustomer_Throws()
    {
        var product = new Product("Tablet", Guid.NewGuid(), new Money(200_000m), new Money(350_000m), new StockQuantity(5m), new StockQuantity(1m));
        var products = new InMemProductRepo(product);
        var sales = new InMemSaleRepo();
        var receivables = new InMemReceivableRepo();
        var unitOfWork = new InMemUoW();

        var useCase = new CreateSaleUseCase(products, sales, unitOfWork, receivables);

        var request = new CreateSaleRequest(
            Guid.NewGuid(),
            null, // no customer
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            new SaleSettlementPlan(200_000m, PaymentMethod.Cash, 150_000m));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_DeferredBalanceAboveConfiguredMaximum_IsRejected()
    {
        // Sale total is 800_000; default maximum deferred balance is 50% (400_000).
        var product = new Product("Laptop", Guid.NewGuid(), new Money(500_000m), new Money(800_000m), new StockQuantity(5m), new StockQuantity(1m));
        var products = new InMemProductRepo(product);
        var sales = new InMemSaleRepo();
        var receivables = new InMemReceivableRepo();
        var unitOfWork = new InMemUoW();
        var customerId = Guid.NewGuid();

        var useCase = new CreateSaleUseCase(products, sales, unitOfWork, receivables);

        var request = new CreateSaleRequest(
            Guid.NewGuid(),
            customerId,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            new SaleSettlementPlan(300_000m, PaymentMethod.Cash, 500_000m));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(request, TestContext.Current.CancellationToken));

        Assert.Contains("50%", ex.Message);
        Assert.Empty(receivables.Stored);
    }

    [Fact]
    public async Task ExecuteAsync_FullPayment_NoReceivedBalance_NoReceivableCreated()
    {
        var product = new Product("Mouse", Guid.NewGuid(), new Money(30_000m), new Money(50_000m), new StockQuantity(10m), new StockQuantity(2m));
        var products = new InMemProductRepo(product);
        var sales = new InMemSaleRepo();
        var receivables = new InMemReceivableRepo();
        var unitOfWork = new InMemUoW();

        var useCase = new CreateSaleUseCase(products, sales, unitOfWork, receivables);

        var request = new CreateSaleRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            new SaleSettlementPlan(50_000m, PaymentMethod.Cash, 0m));

        var result = await useCase.ExecuteAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(0m, result.DeferredBalance);
        Assert.Empty(receivables.Stored);
    }

    private sealed class InMemProductRepo : IProductRepository
    {
        private readonly Product _product;
        public InMemProductRepo(Product product) => _product = product;
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(id == _product.Id ? _product : null);
        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
            => Task.FromResult<Product?>(null);
        public Task AddAsync(Product product, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemSaleRepo : ISaleRepository
    {
        public Task AddAsync(Sale sale, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Sale?>(null);

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class InMemReceivableRepo : IReceivableRepository
    {
        public List<Receivable> Stored { get; } = [];

        public Task AddAsync(Receivable receivable, CancellationToken ct = default)
        {
            Stored.Add(receivable);
            return Task.CompletedTask;
        }

        public Task<Receivable?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Receivable?>(Stored.FirstOrDefault(r => r.Id == id));

        public Task UpdateAsync(Receivable receivable, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemUoW : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}

public sealed class CreateSaleUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenSaleIsValid_PersistsSaleAndReturnsTotals()
    {
        var product = new Product("Sugar", Guid.NewGuid(), new Money(1_000m), new Money(1_400m), new StockQuantity(10m), new StockQuantity(2m));
        var products = new InMemoryProductRepository(product);
        var sales = new InMemorySaleRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new CreateSaleUseCase(products, sales, unitOfWork);

        var result = await useCase.ExecuteAsync(new CreateSaleRequest(
            Guid.NewGuid(),
            null,
            [new CreateSaleItemRequest(product.Id, 2m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 2_800m, 3_000m, null)]), TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, result.SaleId);
        Assert.Equal(2_800m, result.Total);
        Assert.Equal(2_800m, result.CashInDrawer);
        Assert.Single(sales.Sales);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_CapturesUnitCostSnapshotFromProductCostPrice()
    {
        var product = new Product("Coffee", Guid.NewGuid(), new Money(2_000m), new Money(3_000m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var useCase = new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork());

        await useCase.ExecuteAsync(new CreateSaleRequest(
            Guid.NewGuid(),
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 3_000m, 3_000m, null)]), TestContext.Current.CancellationToken);

        var item = sales.Sales.Single().Items.Single();
        Assert.Equal(2_000m, item.UnitCost.Amount); // snapshot of product.CostPrice
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidPlatform_ThrowsAndDoesNotSave()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(500m), new Money(800m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var useCase = new CreateSaleUseCase(new InMemoryProductRepository(product), sales, new InMemoryUnitOfWork());

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new CreateSaleRequest(
                Guid.NewGuid(),
                null,
                [new CreateSaleItemRequest(product.Id, 1m, 0m)],
                [new CreateSalePaymentRequest(PaymentMethod.Transfer, 800m, null, "PayPal")]),
            TestContext.Current.CancellationToken));

        Assert.Empty(sales.Sales);
    }

    [Fact]
    public async Task ExecuteAsync_WithCustomerAndActiveLoyaltyRule_EarnsPointsInSingleSaleCommit()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(500m), new Money(1_000m), new StockQuantity(10m), new StockQuantity(2m));
        var sales = new InMemorySaleRepository();
        var unitOfWork = new InMemoryUnitOfWork();
        var loyalty = new InMemoryLoyaltyRepository { ActiveRule = new LoyaltyRule(0.001m, 1m, 0) };
        var accumulate = new AccumulateLoyaltyPointsUseCase(loyalty, unitOfWork);
        var customerId = Guid.NewGuid();
        var useCase = new CreateSaleUseCase(new InMemoryProductRepository(product), sales, unitOfWork, null, accumulate);

        var result = await useCase.ExecuteAsync(new CreateSaleRequest(
            Guid.NewGuid(),
            customerId,
            [new CreateSaleItemRequest(product.Id, 5m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 5_000m, 5_000m, null)]), TestContext.Current.CancellationToken);

        Assert.Single(sales.Sales);
        Assert.Single(loyalty.Accounts);
        Assert.Equal(customerId, loyalty.Accounts[0].CustomerId);
        LoyaltyTransaction transaction = Assert.Single(loyalty.Transactions);
        Assert.Equal(5, transaction.Points);
        Assert.Equal(result.SaleId, transaction.SaleId);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        private readonly Product _product;

        public InMemoryProductRepository(Product product) => _product = product;

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(id == _product.Id ? _product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult(_product.Barcode == barcode ? _product : null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class InMemorySaleRepository : ISaleRepository
    {
        public List<Sale> Sales { get; } = [];

        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
        {
            Sales.Add(sale);
            return Task.CompletedTask;
        }

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Sales.SingleOrDefault(sale => sale.Id == id));

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class InMemoryLoyaltyRepository : ILoyaltyRepository
    {
        public LoyaltyRule? ActiveRule { get; set; }
        public List<LoyaltyAccount> Accounts { get; } = [];
        public List<LoyaltyTransaction> Transactions { get; } = [];

        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(Accounts.FirstOrDefault(account => account.CustomerId == customerId));

        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken cancellationToken = default)
        {
            Accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default)
        {
            Transactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(Transactions.Where(transaction => transaction.LoyaltyAccountId == loyaltyAccountId).Take(maxCount).ToList());

        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveRule);

        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyRule>>(ActiveRule is null ? [] : [ActiveRule]);

        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default)
        {
            ActiveRule = rule;
            return Task.CompletedTask;
        }

        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken cancellationToken = default)
            => Task.FromResult(Transactions.Where(transaction => transaction.LoyaltyAccountId == loyaltyAccountId).Sum(transaction => transaction.Points));

        public Task<int> GetTotalIssuedPointsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Transactions.Where(transaction => transaction.Points > 0).Sum(transaction => transaction.Points));

        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Accounts.Count);

        public Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(Transactions.Count(transaction => transaction.Points < 0));

        public Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(Transactions.Take(maxCount).ToList());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiscountExceedsLimitAndNotQuoteConversion_Throws()
    {
        // GIVEN
        var product = new Product("Rice", Guid.NewGuid(), new Money(500m), new Money(1_000m), new StockQuantity(10m), new StockQuantity(2m));
        var products = new InMemoryProductRepository(product);
        var sales = new InMemorySaleRepository();
        var unitOfWork = new InMemoryUnitOfWork();

        var settings = new BusinessSettings(
            "Test Business",
            null, null, null, null, null, null, null, false, null, null, null, null, null,
            "COP",
            maximumCashierDiscountPercent: 20m);
        var settingsRepo = new SettingsRepositoryFake(settings);

        var useCase = new CreateSaleUseCase(
            products,
            sales,
            unitOfWork,
            receivables: null,
            accumulatePoints: null,
            redeemPoints: null,
            inventory: null,
            settings: settingsRepo);

        var request = new CreateSaleRequest(
            Guid.NewGuid(),
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 250m, ManualDiscountPercent: 25m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 750m, 1_000m, null)]);

        // WHEN & THEN
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(request, TestContext.Current.CancellationToken));

        Assert.Contains("exceeds the configured maximum of 20%", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiscountExceedsLimitAndIsQuoteConversion_Succeeds()
    {
        // GIVEN
        var product = new Product("Rice", Guid.NewGuid(), new Money(500m), new Money(1_000m), new StockQuantity(10m), new StockQuantity(2m));
        var products = new InMemoryProductRepository(product);
        var sales = new InMemorySaleRepository();
        var unitOfWork = new InMemoryUnitOfWork();

        var settings = new BusinessSettings(
            "Test Business",
            null, null, null, null, null, null, null, false, null, null, null, null, null,
            "COP",
            maximumCashierDiscountPercent: 20m);
        var settingsRepo = new SettingsRepositoryFake(settings);

        var useCase = new CreateSaleUseCase(
            products,
            sales,
            unitOfWork,
            receivables: null,
            accumulatePoints: null,
            redeemPoints: null,
            inventory: null,
            settings: settingsRepo);

        var request = new CreateSaleRequest(
            Guid.NewGuid(),
            null,
            [new CreateSaleItemRequest(product.Id, 1m, 250m, ManualDiscountPercent: 25m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 750m, 1_000m, null)])
        {
            IsQuoteConversion = true
        };

        // WHEN
        var result = await useCase.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // THEN
        Assert.NotNull(result);
        Assert.Single(sales.Sales);
    }

    private sealed class SettingsRepositoryFake : ISettingsRepository
    {
        private readonly BusinessSettings _settings;
        public SettingsRepositoryFake(BusinessSettings settings) => _settings = settings;
        public Task<BusinessSettings?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult<BusinessSettings?>(_settings);
        public Task SaveAsync(BusinessSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
