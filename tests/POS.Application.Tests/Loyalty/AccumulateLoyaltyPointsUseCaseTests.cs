using POS.Application.DTOs.Loyalty;
using POS.Application.UseCases.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using Xunit;

namespace POS.Application.Tests.Loyalty;

public sealed class AccumulateLoyaltyPointsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithActiveRule_CreatesEarnedTransaction()
    {
        var repo = new InMemLoyaltyRepository();
        var uow = new CountingUow();
        var rule = new LoyaltyRule(0.001m, 1m, 0, "Test rule");
        repo.ActiveRule = rule;

        var useCase = new AccumulateLoyaltyPointsUseCase(repo, uow);
        var customerId = Guid.NewGuid();
        var saleId = Guid.NewGuid();

        await useCase.ExecuteAsync(new EarnPointsRequest(customerId, saleId, 5_000m), TestContext.Current.CancellationToken);

        Assert.Single(repo.Transactions);
        Assert.Equal(5, repo.Transactions[0].Points);   // FLOOR(5000 * 0.001)
        Assert.Equal(LoyaltyTransactionType.Earned, repo.Transactions[0].Type);
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoActiveRule_SkipsAccumulation()
    {
        var repo = new InMemLoyaltyRepository { ActiveRule = null };
        var uow = new CountingUow();
        var useCase = new AccumulateLoyaltyPointsUseCase(repo, uow);

        await useCase.ExecuteAsync(new EarnPointsRequest(Guid.NewGuid(), Guid.NewGuid(), 5_000m), TestContext.Current.CancellationToken);

        Assert.Empty(repo.Transactions);
        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCalculatedPointsAreZero_SkipsTransaction()
    {
        var repo = new InMemLoyaltyRepository();
        var uow = new CountingUow();
        // 1 pt per 1000, but total is 500 → FLOOR(0.5) = 0
        repo.ActiveRule = new LoyaltyRule(0.001m, 1m, 0);

        var useCase = new AccumulateLoyaltyPointsUseCase(repo, uow);

        await useCase.ExecuteAsync(new EarnPointsRequest(Guid.NewGuid(), Guid.NewGuid(), 500m), TestContext.Current.CancellationToken);

        Assert.Empty(repo.Transactions);
        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCustomerHasNoAccount_CreatesAccountAndTransaction()
    {
        var repo = new InMemLoyaltyRepository();
        var uow = new CountingUow();
        repo.ActiveRule = new LoyaltyRule(0.001m, 1m, 0);
        // No pre-existing account
        var customerId = Guid.NewGuid();

        var useCase = new AccumulateLoyaltyPointsUseCase(repo, uow);

        await useCase.ExecuteAsync(new EarnPointsRequest(customerId, Guid.NewGuid(), 10_000m), TestContext.Current.CancellationToken);

        Assert.Single(repo.Accounts);
        Assert.Equal(customerId, repo.Accounts[0].CustomerId);
        Assert.Single(repo.Transactions);
    }

    [Fact]
    public async Task RecordForSaleAsync_WithActiveRule_StagesTransactionWithoutSaving()
    {
        var repo = new InMemLoyaltyRepository { ActiveRule = new LoyaltyRule(0.001m, 1m, 0) };
        var uow = new CountingUow();
        var useCase = new AccumulateLoyaltyPointsUseCase(repo, uow);

        bool recorded = await useCase.RecordForSaleAsync(
            new EarnPointsRequest(Guid.NewGuid(), Guid.NewGuid(), 7_500m),
            TestContext.Current.CancellationToken);

        Assert.True(recorded);
        Assert.Single(repo.Transactions);
        Assert.Equal(7, repo.Transactions[0].Points);
        Assert.Equal(0, uow.SaveCount);
    }

    // --- Stubs ---

    private sealed class InMemLoyaltyRepository : ILoyaltyRepository
    {
        public LoyaltyRule? ActiveRule { get; set; }
        public List<LoyaltyAccount> Accounts { get; } = [];
        public List<LoyaltyTransaction> Transactions { get; } = [];
        private int _balance;

        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
            => Task.FromResult(Accounts.FirstOrDefault(a => a.CustomerId == customerId));

        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken ct = default)
        {
            Accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken ct = default)
        {
            Transactions.Add(transaction);
            _balance += transaction.Points;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(Transactions.Take(maxCount).ToList());

        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken ct = default)
            => Task.FromResult(ActiveRule);

        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken ct = default) => Task.CompletedTask;

        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken ct = default)
            => Task.FromResult(_balance);

        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyRule>>([]);

        public Task<int> GetTotalIssuedPointsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Transactions.Where(transaction => transaction.Points > 0).Sum(transaction => transaction.Points));

        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Accounts.Count);

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
