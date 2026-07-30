using POS.Application.UseCases.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class LoyaltyBenefitsViewModelTests
{
    [Fact]
    public async Task LoadAsync_ShowsTheCatalogueIncludingDeactivatedRewards()
    {
        var repo = new FakeLoyaltyRepository();
        repo.Benefits.Add(new LoyaltyBenefit("Asesoría personalizada", 1_000, "Una hora"));
        LoyaltyBenefit retired = new("Mug de regalo", 300);
        retired.Deactivate();
        repo.Benefits.Add(retired);

        var vm = BuildViewModel(repo);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, vm.Benefits.Count);
        Assert.True(vm.HasBenefits);
        Assert.Equal("1.000 pts", vm.Benefits.Single(b => b.Name == "Asesoría personalizada").PointsCostLabel);
        Assert.False(vm.Benefits.Single(b => b.Name == "Mug de regalo").IsActive);
    }

    [Fact]
    public async Task SaveAsync_AcceptsACostTypedWithColombianThousandsSeparator()
    {
        var repo = new FakeLoyaltyRepository();
        var vm = BuildViewModel(repo);
        vm.Name = "Asesoría personalizada";
        vm.PointsCostText = "1.000";

        await vm.SaveAsync(TestContext.Current.CancellationToken);

        LoyaltyBenefit saved = Assert.Single(repo.Benefits);
        Assert.Equal(1_000, saved.PointsCost);
        Assert.Contains("guardado", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("cero")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task SaveAsync_WithAnUnusableCost_ReportsItAndPersistsNothing(string pointsCostText)
    {
        var repo = new FakeLoyaltyRepository();
        var vm = BuildViewModel(repo);
        vm.Name = "Asesoría personalizada";
        vm.PointsCostText = pointsCostText;

        await vm.SaveAsync(TestContext.Current.CancellationToken);

        Assert.Empty(repo.Benefits);
        Assert.Contains("costo en puntos", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveAsync_WithoutAName_ReportsItAndPersistsNothing()
    {
        var repo = new FakeLoyaltyRepository();
        var vm = BuildViewModel(repo);
        vm.PointsCostText = "1.000";

        await vm.SaveAsync(TestContext.Current.CancellationToken);

        Assert.Empty(repo.Benefits);
        Assert.Contains("nombre", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelectingABenefit_LoadsTheEditor_AndSavingUpdatesItInsteadOfCreatingAnother()
    {
        var repo = new FakeLoyaltyRepository();
        repo.Benefits.Add(new LoyaltyBenefit("Asesoría", 1_000, "Una hora"));
        var vm = BuildViewModel(repo);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        vm.SelectedBenefit = vm.Benefits.Single();
        Assert.Equal("Asesoría", vm.Name);
        Assert.Equal("Una hora", vm.Description);
        Assert.Equal("1.000", vm.PointsCostText);
        Assert.True(vm.IsEditingExistingBenefit);

        vm.Name = "Asesoría premium";
        vm.PointsCostText = "1.500";
        await vm.SaveAsync(TestContext.Current.CancellationToken);

        LoyaltyBenefit updated = Assert.Single(repo.Benefits);
        Assert.Equal("Asesoría premium", updated.Name);
        Assert.Equal(1_500, updated.PointsCost);

        // The form clears so the next save creates rather than silently editing the same row again.
        Assert.Null(vm.SelectedBenefit);
        Assert.Equal(string.Empty, vm.Name);
    }

    [Fact]
    public async Task DeleteAsync_OnAClaimedBenefit_ExplainsItWasDeactivatedInstead()
    {
        var repo = new FakeLoyaltyRepository();
        var benefit = new LoyaltyBenefit("Asesoría", 1_000);
        repo.Benefits.Add(benefit);
        repo.ClaimedBenefitIds.Add(benefit.Id);

        var vm = BuildViewModel(repo);
        await vm.LoadAsync(TestContext.Current.CancellationToken);
        vm.SelectedBenefit = vm.Benefits.Single();

        await vm.DeleteAsync(TestContext.Current.CancellationToken);

        Assert.Single(repo.Benefits);
        Assert.Contains("desactivó", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteAsync_WithoutASelection_AsksForOne()
    {
        var repo = new FakeLoyaltyRepository();
        repo.Benefits.Add(new LoyaltyBenefit("Asesoría", 1_000));
        var vm = BuildViewModel(repo);
        await vm.LoadAsync(TestContext.Current.CancellationToken);

        await vm.DeleteAsync(TestContext.Current.CancellationToken);

        Assert.Single(repo.Benefits);
        Assert.Contains("selecciona", vm.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoyaltyModule_ExposesThreeMutuallyExclusivePanes()
    {
        var repo = new FakeLoyaltyRepository();
        var uow = new NoOpUnitOfWork();
        var parent = new LoyaltyViewModel(
            new GetLoyaltyBalanceUseCase(repo),
            new RedeemLoyaltyPointsUseCase(repo, uow),
            benefitsViewModel: BuildViewModel(repo));

        Assert.True(parent.IsTiersListOpen);
        Assert.False(parent.IsBenefitsOpen);
        Assert.True(parent.HasBenefitsModule);

        parent.ShowBenefitsCommand.Execute(null);
        Assert.True(parent.IsBenefitsOpen);
        Assert.False(parent.IsTiersListOpen);
        Assert.False(parent.IsTierEditorOpen);

        parent.ShowTierEditorCommand.Execute(null);
        Assert.True(parent.IsTierEditorOpen);
        Assert.False(parent.IsBenefitsOpen);

        parent.ShowTiersListCommand.Execute(null);
        Assert.True(parent.IsTiersListOpen);
        Assert.False(parent.IsBenefitsOpen);
    }

    [Fact]
    public void LoyaltyModule_WithoutTheCatalogue_HidesTheBenefitsTab()
    {
        var repo = new FakeLoyaltyRepository();
        var uow = new NoOpUnitOfWork();
        var parent = new LoyaltyViewModel(
            new GetLoyaltyBalanceUseCase(repo),
            new RedeemLoyaltyPointsUseCase(repo, uow));

        Assert.False(parent.HasBenefitsModule);
        Assert.False(parent.ShowBenefitsCommand.CanExecute(null));

        parent.ShowBenefitsCommand.Execute(null);
        Assert.False(parent.IsBenefitsOpen);
        Assert.True(parent.IsTiersListOpen);
    }

    private static LoyaltyBenefitsViewModel BuildViewModel(FakeLoyaltyRepository repo)
    {
        var uow = new NoOpUnitOfWork();
        return new LoyaltyBenefitsViewModel(
            new GetLoyaltyBenefitsUseCase(repo),
            new SaveLoyaltyBenefitUseCase(repo, uow),
            new DeleteLoyaltyBenefitUseCase(repo, uow));
    }

    private sealed class FakeLoyaltyRepository : ILoyaltyRepository
    {
        public List<LoyaltyBenefit> Benefits { get; } = [];
        public HashSet<Guid> ClaimedBenefitIds { get; } = [];

        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
            => Task.FromResult<LoyaltyAccount?>(null);
        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);
        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken ct = default) => Task.FromResult<LoyaltyRule?>(null);
        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyRule>>([]);
        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> GetTotalIssuedPointsAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken ct = default) => Task.FromResult(0);
        public Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) => Task.FromResult(0);
        public Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>([]);

        public Task<IReadOnlyList<LoyaltyBenefit>> GetBenefitsAsync(bool activeOnly = false, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyBenefit>>(
                Benefits.Where(benefit => !activeOnly || benefit.IsActive).ToList());

        public Task<LoyaltyBenefit?> GetBenefitByIdAsync(Guid benefitId, CancellationToken ct = default)
            => Task.FromResult(Benefits.FirstOrDefault(benefit => benefit.Id == benefitId));

        public Task AddBenefitAsync(LoyaltyBenefit benefit, CancellationToken ct = default)
        {
            Benefits.Add(benefit);
            return Task.CompletedTask;
        }

        public Task UpdateBenefitAsync(LoyaltyBenefit benefit, CancellationToken ct = default) => Task.CompletedTask;

        public Task DeleteBenefitAsync(Guid benefitId, CancellationToken ct = default)
        {
            Benefits.RemoveAll(benefit => benefit.Id == benefitId);
            return Task.CompletedTask;
        }

        public Task<bool> HasClaimsForBenefitAsync(Guid benefitId, CancellationToken ct = default)
            => Task.FromResult(ClaimedBenefitIds.Contains(benefitId));
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(0);
    }
}
