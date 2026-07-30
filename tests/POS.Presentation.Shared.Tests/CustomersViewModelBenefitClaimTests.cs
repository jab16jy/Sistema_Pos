using POS.Application.DTOs.Customers;
using POS.Application.Interfaces;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Loyalty;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class CustomersViewModelBenefitClaimTests
{
    [Fact]
    public void RedemptionFilter_KeepsOnlyRedemptionRows()
    {
        CustomersViewModel vm = BuildViewModel(out _, out _);
        vm.HistoryEntries.Add(new CustomerHistoryEntry(DateTimeOffset.UtcNow, "Sale", "F-001", 10_000m, "Completed", Guid.NewGuid()));
        vm.HistoryEntries.Add(new CustomerHistoryEntry(DateTimeOffset.UtcNow, "Quote", "C-001", 5_000m, "Open"));
        vm.HistoryEntries.Add(new CustomerHistoryEntry(
            DateTimeOffset.UtcNow, "Redemption", "Asesoría personalizada — 1.000 pts", 0m, "RedemptionPending", null, Guid.NewGuid()));

        vm.HistoryFilter = "redemption";
        CustomerHistoryEntry only = Assert.Single(vm.FilteredHistoryEntries);
        Assert.Equal("Redemption", only.Kind);

        // The other chips must not start showing redemptions.
        vm.HistoryFilter = "sale";
        Assert.Equal("Sale", Assert.Single(vm.FilteredHistoryEntries).Kind);
        vm.HistoryFilter = "quote";
        Assert.Equal("Quote", Assert.Single(vm.FilteredHistoryEntries).Kind);
        vm.HistoryFilter = "all";
        Assert.Equal(3, vm.FilteredHistoryEntries.Count);
    }

    [Fact]
    public async Task ClaimBenefitCommand_IsDisabledUntilTheCustomerCanAffordTheReward()
    {
        CustomersViewModel vm = BuildViewModel(out FakeLoyaltyRepository loyalty, out _, balance: 500);
        loyalty.Benefits.Add(new LoyaltyBenefit("Asesoría personalizada", 1_000));
        loyalty.Benefits.Add(new LoyaltyBenefit("Sticker", 100));

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.SelectedCustomer = vm.Customers.Single();
        await vm.OpenHistoryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(500, vm.LoyaltyPoints);
        Assert.Equal(2, vm.AvailableBenefits.Count);

        vm.SelectedBenefit = vm.AvailableBenefits.Single(b => b.PointsCost == 1_000);
        Assert.False(vm.CanClaimSelectedBenefit);
        Assert.False(vm.ClaimBenefitCommand.CanExecute(null));

        vm.SelectedBenefit = vm.AvailableBenefits.Single(b => b.PointsCost == 100);
        Assert.True(vm.CanClaimSelectedBenefit);
        Assert.True(vm.ClaimBenefitCommand.CanExecute(null));
    }

    [Fact]
    public async Task ClaimBenefitAsync_SpendsThePointsAndSwitchesToTheRedemptionChip()
    {
        CustomersViewModel vm = BuildViewModel(out FakeLoyaltyRepository loyalty, out _, balance: 2_500);
        loyalty.Benefits.Add(new LoyaltyBenefit("Asesoría personalizada", 1_000));

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.SelectedCustomer = vm.Customers.Single();
        await vm.OpenHistoryAsync(TestContext.Current.CancellationToken);
        vm.SelectedBenefit = vm.AvailableBenefits.Single();

        await vm.ClaimBenefitAsync(TestContext.Current.CancellationToken);

        Assert.Single(loyalty.Claims);
        Assert.Equal(1_500, loyalty.Balance);
        Assert.Equal(1_500, vm.LoyaltyPoints);
        Assert.Equal("redemption", vm.HistoryFilter);
        Assert.Contains("pendiente de entrega", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClaimBenefitAsync_WhenTheDomainRefuses_ShowsTheReasonInsteadOfAWrapperError()
    {
        CustomersViewModel vm = BuildViewModel(out FakeLoyaltyRepository loyalty, out _, balance: 2_000);
        var benefit = new LoyaltyBenefit("Asesoría personalizada", 1_000);
        loyalty.Benefits.Add(benefit);

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.SelectedCustomer = vm.Customers.Single();
        await vm.OpenHistoryAsync(TestContext.Current.CancellationToken);
        vm.SelectedBenefit = vm.AvailableBenefits.Single();

        // Retired between picking it and pressing the button.
        benefit.Deactivate();
        await vm.ClaimBenefitAsync(TestContext.Current.CancellationToken);

        Assert.Empty(loyalty.Claims);
        Assert.Equal(2_000, loyalty.Balance);
        Assert.Contains("ya no está disponible", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ClaimAndCancelAreGatedByTheirOwnPermissions()
    {
        CustomersViewModel claimOnly = BuildViewModel(out _, out _, permissions: [PermissionKeys.Loyalty.ClaimBenefits]);
        Assert.True(claimOnly.CanClaimBenefits);
        Assert.False(claimOnly.CanCancelClaims);

        CustomersViewModel admin = BuildViewModel(out _, out _,
            permissions: [PermissionKeys.Loyalty.ClaimBenefits, PermissionKeys.Loyalty.CancelBenefitClaims]);
        Assert.True(admin.CanClaimBenefits);
        Assert.True(admin.CanCancelClaims);

        CustomersViewModel none = BuildViewModel(out _, out _, permissions: []);
        Assert.False(none.CanClaimBenefits);
        Assert.False(none.CanCancelClaims);
    }

    [Fact]
    public async Task CancelClaimAsync_WithoutThePermission_RefusesAndLeavesThePointsSpent()
    {
        CustomersViewModel vm = BuildViewModel(out FakeLoyaltyRepository loyalty, out _, balance: 2_000,
            permissions: [PermissionKeys.Loyalty.ClaimBenefits]);
        loyalty.Benefits.Add(new LoyaltyBenefit("Asesoría personalizada", 1_000));

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.SelectedCustomer = vm.Customers.Single();
        await vm.OpenHistoryAsync(TestContext.Current.CancellationToken);
        vm.SelectedBenefit = vm.AvailableBenefits.Single();
        await vm.ClaimBenefitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1_000, loyalty.Balance);

        var entry = new CustomerHistoryEntry(
            DateTimeOffset.UtcNow, "Redemption", "Asesoría", 0m, "RedemptionPending", null, loyalty.Claims.Single().Id);
        await vm.CancelClaimAsync(entry, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1_000, loyalty.Balance);
        Assert.Contains("permiso", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancelClaimAsync_WithThePermission_ReturnsThePointsToTheCustomer()
    {
        CustomersViewModel vm = BuildViewModel(out FakeLoyaltyRepository loyalty, out _, balance: 2_000,
            permissions: [PermissionKeys.Loyalty.ClaimBenefits, PermissionKeys.Loyalty.CancelBenefitClaims]);
        loyalty.Benefits.Add(new LoyaltyBenefit("Asesoría personalizada", 1_000));

        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.SelectedCustomer = vm.Customers.Single();
        await vm.OpenHistoryAsync(TestContext.Current.CancellationToken);
        vm.SelectedBenefit = vm.AvailableBenefits.Single();
        await vm.ClaimBenefitAsync(TestContext.Current.CancellationToken);

        var entry = new CustomerHistoryEntry(
            DateTimeOffset.UtcNow, "Redemption", "Asesoría", 0m, "RedemptionPending", null, loyalty.Claims.Single().Id);
        await vm.CancelClaimAsync(entry, "Cliente desistió", TestContext.Current.CancellationToken);

        Assert.Equal(2_000, loyalty.Balance);
        Assert.Equal(2_000, vm.LoyaltyPoints);
        Assert.Contains("volvieron al saldo", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CustomersViewModel BuildViewModel(
        out FakeLoyaltyRepository loyalty,
        out Guid customerId,
        int balance = 0,
        IReadOnlyList<string>? permissions = null)
    {
        customerId = Guid.NewGuid();
        loyalty = new FakeLoyaltyRepository(customerId, balance);
        var uow = new NoOpUnitOfWork();
        var lookup = new SingleCustomerLookup(customerId);
        var session = new FakeSession(permissions ?? [PermissionKeys.Loyalty.ClaimBenefits, PermissionKeys.Loyalty.CancelBenefitClaims]);

        return new CustomersViewModel(
            new SaveCustomerUseCase(new ThrowingCustomerRepository(), uow),
            new GetCustomerHistoryUseCase(new EmptyHistory()),
            new SearchCustomersUseCase(lookup),
            new GetLoyaltyBalanceUseCase(loyalty),
            receivableQuery: null,
            applyPayment: null,
            session: session,
            cashierShifts: null,
            getBenefits: new GetLoyaltyBenefitsUseCase(loyalty),
            claimBenefit: new ClaimLoyaltyBenefitUseCase(loyalty, uow),
            deliverClaim: new DeliverLoyaltyBenefitClaimUseCase(loyalty, uow),
            cancelClaim: new CancelLoyaltyBenefitClaimUseCase(loyalty, uow));
    }

    private sealed class FakeSession(IReadOnlyList<string> permissions) : ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = Guid.NewGuid();
        public string Username => "tester";
        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>(permissions, StringComparer.Ordinal);
        public bool IsAuthenticated => true;
    }

    private sealed class EmptyHistory : ICustomerHistoryQueryService
    {
        public Task<CustomerHistoryResult> GetHistoryAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(new CustomerHistoryResult(customerId, []));
    }

    private sealed class SingleCustomerLookup(Guid customerId) : ICustomerLookupQueryService
    {
        public Task<IReadOnlyList<CustomerSearchResult>> SearchAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CustomerSearchResult>>(
                [new CustomerSearchResult(customerId, "Ana Gomez", "123", null, null, null, true)]);
    }

    private sealed class ThrowingCustomerRepository : ICustomerRepository
    {
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Customer?>(null);
        public Task<Customer?> GetByIdentificationAsync(string identification, CancellationToken ct = default) => Task.FromResult<Customer?>(null);
        public Task AddAsync(Customer customer, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Customer customer, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Customer>> SearchAsync(string? term, bool includeInactive, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Customer>>([]);
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeLoyaltyRepository : ILoyaltyRepository
    {
        private readonly LoyaltyAccount _account;

        public FakeLoyaltyRepository(Guid customerId, int balance)
        {
            _account = new LoyaltyAccount(customerId);
            Balance = balance;
        }

        public int Balance { get; private set; }
        public List<LoyaltyBenefit> Benefits { get; } = [];
        public List<LoyaltyBenefitClaim> Claims { get; } = [];
        public List<LoyaltyTransaction> Transactions { get; } = [];

        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
            => Task.FromResult(_account.CustomerId == customerId ? _account : null);
        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken ct = default) => Task.CompletedTask;

        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken ct = default)
        {
            Transactions.Add(transaction);
            Balance += transaction.Points;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);
        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken ct = default) => Task.FromResult<LoyaltyRule?>(null);
        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyRule>>([]);
        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken ct = default) => Task.FromResult(Balance);
        public Task<int> GetTotalIssuedPointsAsync(CancellationToken ct = default) => Task.FromResult(Balance);
        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken ct = default) => Task.FromResult(1);
        public Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);

        public Task<IReadOnlyList<LoyaltyBenefit>> GetBenefitsAsync(bool activeOnly = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyBenefit>>(Benefits.Where(b => !activeOnly || b.IsActive).ToList());
        public Task<LoyaltyBenefit?> GetBenefitByIdAsync(Guid benefitId, CancellationToken ct = default)
            => Task.FromResult(Benefits.FirstOrDefault(b => b.Id == benefitId));

        public Task AddClaimAsync(LoyaltyBenefitClaim claim, CancellationToken ct = default)
        {
            Claims.Add(claim);
            return Task.CompletedTask;
        }

        public Task UpdateClaimAsync(LoyaltyBenefitClaim claim, CancellationToken ct = default) => Task.CompletedTask;
        public Task<LoyaltyBenefitClaim?> GetClaimByIdAsync(Guid claimId, CancellationToken ct = default)
            => Task.FromResult(Claims.FirstOrDefault(c => c.Id == claimId));
        public Task<IReadOnlyList<LoyaltyBenefitClaim>> GetClaimsByAccountAsync(Guid loyaltyAccountId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyBenefitClaim>>(Claims);
        public Task<LoyaltyTransaction?> GetTransactionByIdAsync(Guid transactionId, CancellationToken ct = default)
            => Task.FromResult(Transactions.FirstOrDefault(t => t.Id == transactionId));
    }
}
