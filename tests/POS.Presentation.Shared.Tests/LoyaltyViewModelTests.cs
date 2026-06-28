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
        private readonly LoyaltyRule? _activeRule;

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
            => Task.CompletedTask;

        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);
    }
}
