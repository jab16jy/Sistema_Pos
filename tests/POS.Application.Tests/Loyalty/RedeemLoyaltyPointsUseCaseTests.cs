using POS.Application.DTOs.Loyalty;
using POS.Application.UseCases.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using Xunit;

namespace POS.Application.Tests.Loyalty;

public sealed class RedeemLoyaltyPointsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithSufficientBalance_CreatesRedeemedTransactionAndReturnsDiscount()
    {
        var customerId = Guid.NewGuid();
        var account = new LoyaltyAccount(customerId);
        var rule = new LoyaltyRule(0.001m, 1000m, 0); // 1 pt = 1000 COP
        var repo = new InMemLoyaltyRepository(account, rule, balance: 20);
        var uow = new CountingUow();

        var useCase = new RedeemLoyaltyPointsUseCase(repo, uow);

        var result = await useCase.ExecuteAsync(new RedeemPointsRequest(customerId, 10, SaleTotal: 100_000m), TestContext.Current.CancellationToken);

        Assert.Equal(10, result.PointsRedeemed);
        Assert.Equal(10_000m, result.DiscountAmount); // 10 pts * 1000 COP
        Assert.Single(repo.Transactions);
        Assert.Equal(-10, repo.Transactions[0].Points);
        Assert.Equal(LoyaltyTransactionType.Redeemed, repo.Transactions[0].Type);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithInsufficientBalance_Throws()
    {
        var customerId = Guid.NewGuid();
        var account = new LoyaltyAccount(customerId);
        var rule = new LoyaltyRule(0.001m, 1m, 0);
        var repo = new InMemLoyaltyRepository(account, rule, balance: 5);
        var uow = new CountingUow();

        var useCase = new RedeemLoyaltyPointsUseCase(repo, uow);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(new RedeemPointsRequest(customerId, 10, SaleTotal: 100_000m), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_WithNegativeBalance_RejectsFurtherRedemption()
    {
        var customerId = Guid.NewGuid();
        var account = new LoyaltyAccount(customerId);
        var repo = new InMemLoyaltyRepository(account, new LoyaltyRule(0.001m, 1m, 0), balance: -25);
        var useCase = new RedeemLoyaltyPointsUseCase(repo, new CountingUow());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(new RedeemPointsRequest(customerId, 1, SaleTotal: 100m), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoActiveRule_Throws()
    {
        var customerId = Guid.NewGuid();
        var account = new LoyaltyAccount(customerId);
        var repo = new InMemLoyaltyRepository(account, activeRule: null, balance: 100);
        var uow = new CountingUow();

        var useCase = new RedeemLoyaltyPointsUseCase(repo, uow);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(new RedeemPointsRequest(customerId, 10, SaleTotal: 100_000m), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExecuteAsync_BelowMinimumRedeemPoints_Throws()
    {
        var customerId = Guid.NewGuid();
        var account = new LoyaltyAccount(customerId);
        var rule = new LoyaltyRule(0.001m, 1m, minimumRedeemPoints: 50);
        var repo = new InMemLoyaltyRepository(account, rule, balance: 100);
        var uow = new CountingUow();

        var useCase = new RedeemLoyaltyPointsUseCase(repo, uow);

        // Requesting 10 pts when minimum is 50 should throw
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(new RedeemPointsRequest(customerId, 10, SaleTotal: 100_000m), TestContext.Current.CancellationToken));
    }

    // --- Stubs ---

    [Fact]
    public async Task ExecuteAsync_WhenDiscountExceedsSaleTotal_CapsToSaleTotal()
    {
        var customerId = Guid.NewGuid();
        var account = new LoyaltyAccount(customerId);
        // 1 pt = 1_000 COP, minimum redeem = 1
        var rule = new LoyaltyRule(0.001m, 1_000m, minimumRedeemPoints: 1);
        var repo = new InMemLoyaltyRepository(account, rule, balance: 100);
        var uow = new CountingUow();

        var useCase = new RedeemLoyaltyPointsUseCase(repo, uow);

        // Requesting 10 pts would give 10_000 COP discount, but sale total is only 5_500
        // Max whole points that fit: floor(5500 / 1000) = 5 pts = 5_000 COP
        var result = await useCase.ExecuteAsync(
            new RedeemPointsRequest(customerId, 10, SaleTotal: 5_500m),
            TestContext.Current.CancellationToken);

        Assert.Equal(5, result.PointsRedeemed);
        Assert.Equal(5_000m, result.DiscountAmount);
        Assert.Single(repo.Transactions);
        Assert.Equal(-5, repo.Transactions[0].Points);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDiscountExceedsSaleTotal_AndCappedBelowMinimum_Throws()
    {
        var customerId = Guid.NewGuid();
        var account = new LoyaltyAccount(customerId);
        // 1 pt = 1_000 COP, minimum redeem = 5
        var rule = new LoyaltyRule(0.001m, 1_000m, minimumRedeemPoints: 5);
        var repo = new InMemLoyaltyRepository(account, rule, balance: 100);
        var uow = new CountingUow();

        var useCase = new RedeemLoyaltyPointsUseCase(repo, uow);

        // Requesting 10 pts = 10_000 COP discount, sale total = 3_000
        // Max whole points that fit: floor(3000 / 1000) = 3, but minimum is 5 → throw
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(
                new RedeemPointsRequest(customerId, 10, SaleTotal: 3_000m),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PreviewAsync_ComputesDiscountWithoutSpendingPoints()
    {
        var customerId = Guid.NewGuid();
        var account = new LoyaltyAccount(customerId);
        var rule = new LoyaltyRule(0.001m, 1000m, 0);
        var repo = new InMemLoyaltyRepository(account, rule, balance: 20);
        var uow = new CountingUow();

        var useCase = new RedeemLoyaltyPointsUseCase(repo, uow);

        var result = await useCase.PreviewAsync(
            new RedeemPointsRequest(customerId, 10, SaleTotal: 100_000m), TestContext.Current.CancellationToken);

        Assert.Equal(10, result.PointsRedeemed);
        Assert.Equal(10_000m, result.DiscountAmount);
        // Preview must NOT persist anything — points are only spent when the sale is confirmed.
        Assert.Empty(repo.Transactions);
        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public async Task RecordForSaleAsync_StagesRedemptionLinkedToSale_WithoutSaving()
    {
        var customerId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var account = new LoyaltyAccount(customerId);
        var rule = new LoyaltyRule(0.001m, 1000m, 0);
        var repo = new InMemLoyaltyRepository(account, rule, balance: 20);
        var uow = new CountingUow();

        var useCase = new RedeemLoyaltyPointsUseCase(repo, uow);

        var result = await useCase.RecordForSaleAsync(
            new RedeemPointsRequest(customerId, 10, SaleTotal: 100_000m), saleId, TestContext.Current.CancellationToken);

        Assert.Equal(10, result.PointsRedeemed);
        Assert.Equal(10_000m, result.DiscountAmount);
        Assert.Single(repo.Transactions);
        Assert.Equal(-10, repo.Transactions[0].Points);
        Assert.Equal(LoyaltyTransactionType.Redeemed, repo.Transactions[0].Type);
        // Redemption is tied to the sale so it can be reconciled and rolled back with it.
        Assert.Equal(saleId, repo.Transactions[0].SaleId);
        // No save here — the sale's unit of work commits the deduction atomically with the sale.
        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTierDoesNotAllowRedemption_ThrowsAndSavesNothing()
    {
        var customerId = Guid.NewGuid();
        var account = new LoyaltyAccount(customerId);
        var rule = new LoyaltyRule(0.001m, 1000m, 0);
        var repo = new InMemLoyaltyRepository(account, rule, balance: 500,
            tiers: [new LoyaltyTier("Plata", minPoints: 100, allowsRedemption: false)]);
        var uow = new CountingUow();

        var useCase = new RedeemLoyaltyPointsUseCase(repo, uow);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(new RedeemPointsRequest(customerId, 10, SaleTotal: 100_000m), TestContext.Current.CancellationToken));
        Assert.Empty(repo.Transactions);
        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCustomerReachesNoTier_ThrowsBecauseRedemptionIsATierBenefit()
    {
        var customerId = Guid.NewGuid();
        var account = new LoyaltyAccount(customerId);
        var rule = new LoyaltyRule(0.001m, 1000m, 0);
        var repo = new InMemLoyaltyRepository(account, rule, balance: 50,
            tiers: [new LoyaltyTier("Oro", minPoints: 1000, allowsRedemption: true)]);

        var useCase = new RedeemLoyaltyPointsUseCase(repo, new CountingUow());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(new RedeemPointsRequest(customerId, 10, SaleTotal: 100_000m), TestContext.Current.CancellationToken));
        Assert.Empty(repo.Transactions);
    }

    private sealed class InMemLoyaltyRepository : ILoyaltyRepository
    {
        private readonly LoyaltyAccount? _account;
        private readonly LoyaltyRule? _activeRule;
        private readonly int _balance;
        private readonly IReadOnlyList<LoyaltyTier> _tiers;

        public List<LoyaltyTransaction> Transactions { get; } = [];

        public InMemLoyaltyRepository(LoyaltyAccount? account, LoyaltyRule? activeRule, int balance, IReadOnlyList<LoyaltyTier>? tiers = null)
        {
            _account = account;
            _activeRule = activeRule;
            _balance = balance;
            // Redemption is gated on a tier that allows it, so the default fixture supplies one that
            // does — tests about capping, minimums, and balances stay about those rules.
            _tiers = tiers ?? [new LoyaltyTier("Base", minPoints: 0, allowsRedemption: true)];
        }

        public Task<IReadOnlyList<LoyaltyTier>> GetTiersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_tiers);

        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
            => Task.FromResult(_account?.CustomerId == customerId ? _account : null);

        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken ct = default) => Task.CompletedTask;

        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken ct = default)
        {
            Transactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);

        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken ct = default)
            => Task.FromResult(_activeRule);

        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken ct = default) => Task.CompletedTask;

        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken ct = default)
            => Task.FromResult(_balance);

        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyRule>>([]);

        public Task<int> GetTotalIssuedPointsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Transactions.Where(transaction => transaction.Points > 0).Sum(transaction => transaction.Points));

        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_account is null ? 0 : 1);

        public Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(Transactions.Count(transaction => transaction.Points < 0));

        public Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(Transactions.Take(maxCount).ToList());
    }

    private sealed class CountingUow : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
