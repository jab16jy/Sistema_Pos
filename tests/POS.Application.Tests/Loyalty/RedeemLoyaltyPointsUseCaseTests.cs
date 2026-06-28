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

    private sealed class InMemLoyaltyRepository : ILoyaltyRepository
    {
        private readonly LoyaltyAccount? _account;
        private readonly LoyaltyRule? _activeRule;
        private readonly int _balance;

        public List<LoyaltyTransaction> Transactions { get; } = [];

        public InMemLoyaltyRepository(LoyaltyAccount? account, LoyaltyRule? activeRule, int balance)
        {
            _account = account;
            _activeRule = activeRule;
            _balance = balance;
        }

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
