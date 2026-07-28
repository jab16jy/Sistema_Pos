using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Loyalty;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests.Loyalty;

/// <summary>
/// End-to-end tender behaviour of loyalty redemption through CreateSaleUseCase:
/// redemption is applied as a non-money LoyaltyPoints payment covering part of the total,
/// the sale total/tax stay full, and the point deduction commits atomically with the sale.
/// </summary>
public sealed class CreateSaleWithRedemptionTests
{
    [Fact]
    public async Task ExecuteAsync_WithRedemption_AddsLoyaltyTenderAndRecordsRedemptionLinkedToSale()
    {
        var customerId = Guid.NewGuid();
        // Product priced 10_000; 1 unit → sale total 10_000.
        var product = new Product("Item", Guid.NewGuid(), new Money(4_000m), new Money(10_000m), new StockQuantity(10m), new StockQuantity(1m));
        var products = new InMemProductRepo(product);
        var sales = new InMemSaleRepo();
        var uow = new CountingUoW();
        // 1 pt = 100 COP → 10 pts = 1_000 COP discount; balance 50; no minimum.
        var loyalty = new InMemLoyaltyRepo(new LoyaltyAccount(customerId), new LoyaltyRule(0.001m, 100m, 0), balance: 50);
        var redeem = new RedeemLoyaltyPointsUseCase(loyalty, uow);
        var useCase = new CreateSaleUseCase(products, sales, uow, null, null, redeem, authorizer: TestAuthorizer.Create());

        // Cashier pays the net (10_000 - 1_000 = 9_000); the loyalty tender covers the rest.
        var result = await useCase.ExecuteAsync(new CreateSaleRequest(
            customerId,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            [new CreateSalePaymentRequest(PaymentMethod.Cash, 9_000m, 9_000m, null)],
            redeemLoyaltyPoints: 10), TestContext.Current.CancellationToken);

        Sale sale = Assert.Single(sales.Sales);
        // Sale total and tax stay full — redemption is a tender, not a discount.
        Assert.Equal(10_000m, result.Total);
        Assert.Equal(10_000m, sale.Total().Amount);
        // Two tenders: cash 9_000 + loyalty 1_000.
        Assert.Equal(2, sale.Payments.Count);
        Payment loyaltyTender = Assert.Single(sale.Payments, p => p.Method == PaymentMethod.LoyaltyPoints);
        Assert.Equal(1_000m, loyaltyTender.Amount.Amount);
        // Points (non-money) are excluded from the drawer.
        Assert.Equal(9_000m, result.CashInDrawer);
        // Redemption is recorded, linked to THIS sale, with negative points.
        LoyaltyTransaction txn = Assert.Single(loyalty.Transactions);
        Assert.Equal(-10, txn.Points);
        Assert.Equal(LoyaltyTransactionType.Redeemed, txn.Type);
        Assert.Equal(sale.Id, txn.SaleId);
        // Atomic: the sale and the point deduction commit in a single SaveChanges.
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_RedemptionWithSettlementPlan_ThrowsAndDoesNotSave()
    {
        var customerId = Guid.NewGuid();
        var product = new Product("Item", Guid.NewGuid(), new Money(4_000m), new Money(10_000m), new StockQuantity(10m), new StockQuantity(1m));
        var sales = new InMemSaleRepo();
        var uow = new CountingUoW();
        var loyalty = new InMemLoyaltyRepo(new LoyaltyAccount(customerId), new LoyaltyRule(0.001m, 100m, 0), balance: 50);
        var useCase = new CreateSaleUseCase(new InMemProductRepo(product), sales, uow, null, null, new RedeemLoyaltyPointsUseCase(loyalty, uow), authorizer: TestAuthorizer.Create());

        var request = new CreateSaleRequest(
            customerId,
            [new CreateSaleItemRequest(product.Id, 1m, 0m)],
            new SaleSettlementPlan(5_000m, PaymentMethod.Cash, 5_000m))
        {
            RedeemLoyaltyPoints = 10,
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(request, TestContext.Current.CancellationToken));

        Assert.Empty(sales.Sales);
        Assert.Empty(loyalty.Transactions);
        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSaleTotalBelowOnePointValue_ThrowsAndDoesNotSave()
    {
        var customerId = Guid.NewGuid();
        // Product priced 50; 1 pt = 100 COP, so no whole point fits → capped to 0.
        var product = new Product("Candy", Guid.NewGuid(), new Money(20m), new Money(50m), new StockQuantity(10m), new StockQuantity(1m));
        var sales = new InMemSaleRepo();
        var uow = new CountingUoW();
        var loyalty = new InMemLoyaltyRepo(new LoyaltyAccount(customerId), new LoyaltyRule(0.001m, 100m, 0), balance: 50);
        var useCase = new CreateSaleUseCase(new InMemProductRepo(product), sales, uow, null, null, new RedeemLoyaltyPointsUseCase(loyalty, uow), authorizer: TestAuthorizer.Create());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(new CreateSaleRequest(
                customerId,
                [new CreateSaleItemRequest(product.Id, 1m, 0m)],
                [new CreateSalePaymentRequest(PaymentMethod.Cash, 50m, 50m, null)],
                redeemLoyaltyPoints: 10), TestContext.Current.CancellationToken));

        Assert.Empty(sales.Sales);
        Assert.Empty(loyalty.Transactions);
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
        public List<Sale> Sales { get; } = [];
        public Task AddAsync(Sale sale, CancellationToken ct = default)
        {
            Sales.Add(sale);
            return Task.CompletedTask;
        }
        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Sales.SingleOrDefault(s => s.Id == id));
        public Task<DashboardSummaryData> GetDashboardSummaryAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class CountingUoW : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class InMemLoyaltyRepo : ILoyaltyRepository
    {
        private readonly LoyaltyAccount _account;
        private readonly LoyaltyRule? _rule;
        private readonly int _balance;

        public List<LoyaltyTransaction> Transactions { get; } = [];

        public InMemLoyaltyRepo(LoyaltyAccount account, LoyaltyRule? rule, int balance)
        {
            _account = account;
            _rule = rule;
            _balance = balance;
        }

        // Redemption is gated on a tier that grants it; this fixture supplies one so the tests stay
        // about the sale/redemption wiring.
        public Task<IReadOnlyList<LoyaltyTier>> GetTiersAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTier>>([new LoyaltyTier("Base", minPoints: 0, allowsRedemption: true)]);

        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
            => Task.FromResult(_account.CustomerId == customerId ? _account : null);
        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken ct = default)
        {
            Transactions.Add(transaction);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);
        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken ct = default) => Task.FromResult(_rule);
        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyRule>>([]);
        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken ct = default) => Task.FromResult(_balance);
        public Task<int> GetTotalIssuedPointsAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken ct = default) => Task.FromResult(1);
        public Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
            => Task.FromResult(Transactions.Count(t => t.Points < 0));
        public Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(Transactions.Take(maxCount).ToList());
    }
}
