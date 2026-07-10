using POS.Application.DTOs.Customers;
using POS.Application.DTOs.Loyalty;
using POS.Application.UseCases.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class LoyaltyViewModelTests
{
    private static CustomerResult MakeCustomer(Guid? id = null, string name = "Test Customer")
        => new(id ?? Guid.NewGuid(), name, "12345", null, null, null, true);

    [Fact]
    public async Task LoadBalanceCommand_SetsBalanceAndPopulatesTransactions()
    {
        var customerId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var customer = MakeCustomer(customerId);

        var account = new LoyaltyAccount(customerId);
        // Use reflection to set Id for test purposes
        SetEntityId(account, accountId);

        var transactions = new List<LoyaltyTransaction>
        {
            new(accountId, Domain.Enums.LoyaltyTransactionType.Earned, 100, Guid.NewGuid(), "Earned from sale"),
            new(accountId, Domain.Enums.LoyaltyTransactionType.Redeemed, -20, null, "Redeemed 20 pts")
        };

        var repo = new FakeLoyaltyRepository(account, transactions, balance: 80);
        var getBalance = new GetLoyaltyBalanceUseCase(repo);
        var redeem = new RedeemLoyaltyPointsUseCase(repo, new FakeUnitOfWork());
        var vm = new LoyaltyViewModel(getBalance, redeem) { SelectedCustomer = customer };

        await vm.LoadBalanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(80, vm.Balance);
        Assert.Equal(2, vm.Transactions.Count);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadBalanceCommand_WithNoAccount_SetsBalanceZeroAndEmptyTransactions()
    {
        var customer = MakeCustomer();
        var repo = new FakeLoyaltyRepository(null, [], balance: 0);
        var getBalance = new GetLoyaltyBalanceUseCase(repo);
        var redeem = new RedeemLoyaltyPointsUseCase(repo, new FakeUnitOfWork());
        var vm = new LoyaltyViewModel(getBalance, redeem) { SelectedCustomer = customer };

        await vm.LoadBalanceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, vm.Balance);
        Assert.Empty(vm.Transactions);
        Assert.Contains("no tiene cuenta", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RedeemCommand_OnSuccess_SetsMessageWithConfirmation()
    {
        var customerId = Guid.NewGuid();
        var customer = MakeCustomer(customerId);
        var account = new LoyaltyAccount(customerId);
        var rule = new LoyaltyRule(1m, 500m, 10);

        // After redemption, balance drops from 100 to 50
        var repo = new FakeLoyaltyRepository(account, [], balance: 50, activeRule: rule);
        var getBalance = new GetLoyaltyBalanceUseCase(repo);
        var redeem = new RedeemLoyaltyPointsUseCase(repo, new FakeUnitOfWork());
        var vm = new LoyaltyViewModel(getBalance, redeem)
        {
            SelectedCustomer = customer,
            PointsToRedeem = 50
        };

        await vm.RedeemAsync(TestContext.Current.CancellationToken);

        // After redeem, LoadBalanceAsync is called which updates balance and message
        Assert.Equal(50, vm.Balance);
        Assert.Equal(0, vm.PointsToRedeem);
        Assert.Contains("Saldo", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RedeemCommand_OnFailure_SetsErrorMessageWithoutCrash()
    {
        var customerId = Guid.NewGuid();
        var customer = MakeCustomer(customerId);
        var account = new LoyaltyAccount(customerId);
        var rule = new LoyaltyRule(1m, 500m, 10);

        var repo = new FakeLoyaltyRepository(account, [], balance: 5, activeRule: rule);
        var getBalance = new GetLoyaltyBalanceUseCase(repo);
        var redeem = new RedeemLoyaltyPointsUseCase(repo, new FakeUnitOfWork());
        var vm = new LoyaltyViewModel(getBalance, redeem)
        {
            SelectedCustomer = customer,
            PointsToRedeem = 50
        };

        await vm.RedeemAsync(TestContext.Current.CancellationToken);

        Assert.Contains("Insufficient", vm.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadProgramAsync_UsesPersistedOverviewInsteadOfMockKpis()
    {
        var customerId = Guid.NewGuid();
        var account = new LoyaltyAccount(customerId);
        var transactions = new List<LoyaltyTransaction>
        {
            new(account.Id, Domain.Enums.LoyaltyTransactionType.Earned, 120, Guid.NewGuid(), "Venta registrada"),
            new(account.Id, Domain.Enums.LoyaltyTransactionType.Redeemed, -20, null, "Canje en caja")
        };
        var rule = new LoyaltyRule(0.002m, 15m, 50, "Regla real");
        var repo = new FakeLoyaltyRepository(account, transactions, balance: 100, activeRule: rule);
        var vm = new LoyaltyViewModel(
            new GetLoyaltyBalanceUseCase(repo),
            new RedeemLoyaltyPointsUseCase(repo, new FakeUnitOfWork()),
            new GetLoyaltyProgramOverviewUseCase(repo));

        await vm.LoadProgramAsync(TestContext.Current.CancellationToken);

        Assert.Equal("120", vm.TotalPointsIssued);
        Assert.Equal("1", vm.EnrolledCustomers);
        Assert.Equal("1", vm.RedemptionsThisMonth);
        Assert.Equal(0.002m, vm.PointsPerCurrencyUnit);
        Assert.Equal("Regla real", vm.RuleDescription);
        Assert.Equal(2, vm.ProgramTransactions.Count);
    }

    [Fact]
    public async Task SaveRulesAsync_PersistsRuleThroughUseCase()
    {
        var repo = new FakeLoyaltyRepository(null, [], balance: 0);
        var unitOfWork = new FakeUnitOfWork();
        var vm = new LoyaltyViewModel(
            new GetLoyaltyBalanceUseCase(repo),
            new RedeemLoyaltyPointsUseCase(repo, unitOfWork),
            new GetLoyaltyProgramOverviewUseCase(repo),
            new SetLoyaltyRuleUseCase(repo, unitOfWork))
        {
            PointsPerCurrencyUnit = 0.001m,
            RedemptionValuePerPoint = 10m,
            MinimumRedeemPoints = 100,
            RuleDescription = "Nueva regla real"
        };

        await vm.SaveRulesAsync(TestContext.Current.CancellationToken);

        var saved = Assert.Single(repo.Rules);
        Assert.Equal("Nueva regla real", saved.Description);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Contains("guardadas", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveTierAsync_CreatesTierWithPersistedBenefits()
    {
        var repo = new FakeLoyaltyRepository(null, [], balance: 0);
        var unitOfWork = new FakeUnitOfWork();
        var vm = new LoyaltyViewModel(
            new GetLoyaltyBalanceUseCase(repo),
            new RedeemLoyaltyPointsUseCase(repo, unitOfWork),
            getTiers: new GetLoyaltyTiersUseCase(repo),
            createTier: new CreateLoyaltyTierUseCase(repo, unitOfWork),
            updateTier: new UpdateLoyaltyTierUseCase(repo, unitOfWork),
            deleteTier: new DeleteLoyaltyTierUseCase(repo, unitOfWork))
        {
            TierName = "Oro",
            TierMinimumPoints = 1_000,
            TierDiscountPercent = 10m,
            TierPointsMultiplier = 1.5m
        };

        await vm.SaveTierAsync(TestContext.Current.CancellationToken);

        var tier = Assert.Single(vm.Tiers);
        Assert.Equal("Oro", tier.Name);
        Assert.Equal("10% descuento · 1,5x puntos", tier.BenefitDescription);
        Assert.Contains("creado", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void SetEntityId(object entity, Guid id)
    {
        var prop = entity.GetType().GetProperty("Id");
        if (prop is not null && prop.CanWrite)
        {
            prop.SetValue(entity, id);
        }
        else
        {
            // Try private setter via reflection
            var field = entity.GetType().GetField("<Id>k__BackingField",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(entity, id);
        }
    }

    // ─────────────────────────────────────────────────────
    // Fakes
    // ─────────────────────────────────────────────────────

    private sealed class FakeLoyaltyRepository : ILoyaltyRepository
    {
        private readonly LoyaltyAccount? _account;
        private readonly IReadOnlyList<LoyaltyTransaction> _transactions;
        private readonly int _balance;
        private LoyaltyRule? _activeRule;

        public List<LoyaltyRule> Rules { get; } = [];
        public List<LoyaltyTier> Tiers { get; } = [];

        public FakeLoyaltyRepository(
            LoyaltyAccount? account,
            IReadOnlyList<LoyaltyTransaction> transactions,
            int balance,
            LoyaltyRule? activeRule = null)
        {
            _account = account;
            _transactions = transactions;
            _balance = balance;
            _activeRule = activeRule;
            if (activeRule is not null)
            {
                Rules.Add(activeRule);
            }
        }

        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
            => Task.FromResult(_account?.CustomerId == customerId ? _account : null);

        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task AddTransactionAsync(LoyaltyTransaction tx, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<int> GetBalanceAsync(Guid accountId, CancellationToken ct = default)
            => Task.FromResult(_balance);

        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken ct = default)
            => Task.FromResult(_activeRule);

        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid accountId, int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult(_transactions);

        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken ct = default)
        {
            Rules.Add(rule);
            _activeRule = rule;
            return Task.CompletedTask;
        }

        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyRule>>(Rules.Take(maxCount).ToList());

        public Task<int> GetTotalIssuedPointsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_transactions.Where(transaction => transaction.Points > 0).Sum(transaction => transaction.Points));

        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_account is null ? 0 : 1);

        public Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(_transactions.Count(transaction => transaction.Points < 0));

        public Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(_transactions.Take(maxCount).ToList());

        public Task<IReadOnlyList<LoyaltyTier>> GetTiersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTier>>(Tiers.OrderBy(tier => tier.MinPoints).ToList());

        public Task<LoyaltyTier?> GetTierByIdAsync(Guid tierId, CancellationToken cancellationToken = default)
            => Task.FromResult<LoyaltyTier?>(Tiers.SingleOrDefault(tier => tier.Id == tierId));

        public Task AddTierAsync(LoyaltyTier tier, CancellationToken cancellationToken = default)
        {
            Tiers.Add(tier);
            return Task.CompletedTask;
        }

        public Task UpdateTierAsync(LoyaltyTier tier, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteTierAsync(Guid tierId, CancellationToken cancellationToken = default)
        {
            Tiers.RemoveAll(tier => tier.Id == tierId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
