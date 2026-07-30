using POS.Application.DTOs.Loyalty;
using POS.Application.UseCases.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using Xunit;

namespace POS.Application.Tests.Loyalty;

public sealed class LoyaltyBenefitClaimUseCaseTests
{
    [Fact]
    public async Task ClaimAsync_WithSufficientBalance_SpendsPointsAndOpensAPendingClaim()
    {
        var customerId = Guid.NewGuid();
        var repo = new InMemLoyaltyRepository(customerId, balance: 2_500);
        var benefit = repo.AddBenefit("Asesoría personalizada", 1_000);
        var uow = new CountingUow();

        LoyaltyBenefitClaimResult claim = await new ClaimLoyaltyBenefitUseCase(repo, uow)
            .ExecuteAsync(new ClaimLoyaltyBenefitRequest(customerId, benefit.Id), TestContext.Current.CancellationToken);

        Assert.Equal(LoyaltyBenefitClaimStatus.Pending, claim.Status);
        Assert.Equal(1_000, claim.PointsSpent);
        Assert.Equal("Asesoría personalizada", claim.BenefitName);
        Assert.Equal(1_500, repo.Balance);

        LoyaltyTransaction spend = Assert.Single(repo.Transactions);
        Assert.Equal(LoyaltyTransactionType.BenefitRedeemed, spend.Type);
        Assert.Equal(-1_000, spend.Points);
        Assert.Null(spend.SaleId);

        // The spend and the claim must land in the same unit of work.
        Assert.Equal(1, uow.SaveCount);
    }

    [Fact]
    public async Task ClaimAsync_WithInsufficientBalance_ThrowsAndPersistsNothing()
    {
        var customerId = Guid.NewGuid();
        var repo = new InMemLoyaltyRepository(customerId, balance: 999);
        var benefit = repo.AddBenefit("Asesoría personalizada", 1_000);
        var uow = new CountingUow();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClaimLoyaltyBenefitUseCase(repo, uow)
            .ExecuteAsync(new ClaimLoyaltyBenefitRequest(customerId, benefit.Id), TestContext.Current.CancellationToken));

        Assert.Empty(repo.Transactions);
        Assert.Empty(repo.Claims);
        Assert.Equal(0, uow.SaveCount);
        Assert.Equal(999, repo.Balance);
    }

    [Fact]
    public async Task ClaimAsync_OnAnInactiveBenefit_Throws()
    {
        var customerId = Guid.NewGuid();
        var repo = new InMemLoyaltyRepository(customerId, balance: 5_000);
        var benefit = repo.AddBenefit("Asesoría personalizada", 1_000);
        benefit.Deactivate();
        var uow = new CountingUow();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClaimLoyaltyBenefitUseCase(repo, uow)
            .ExecuteAsync(new ClaimLoyaltyBenefitRequest(customerId, benefit.Id), TestContext.Current.CancellationToken));

        Assert.Empty(repo.Claims);
        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public async Task ClaimAsync_WithoutALoyaltyAccount_Throws()
    {
        var repo = new InMemLoyaltyRepository(Guid.NewGuid(), balance: 5_000);
        var benefit = repo.AddBenefit("Asesoría personalizada", 1_000);
        var uow = new CountingUow();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClaimLoyaltyBenefitUseCase(repo, uow)
            .ExecuteAsync(new ClaimLoyaltyBenefitRequest(Guid.NewGuid(), benefit.Id), TestContext.Current.CancellationToken));

        Assert.Equal(0, uow.SaveCount);
    }

    [Fact]
    public async Task ClaimAsync_IgnoresTheProgramMinimumAndTheTierRedemptionGate()
    {
        // Benefit redemption is independent of the discount path by product decision: a 1-point
        // reward is claimable even when the rule demands 500 points for a discount and no tier
        // allows redemption at all.
        var customerId = Guid.NewGuid();
        var repo = new InMemLoyaltyRepository(customerId, balance: 10)
        {
            ActiveRule = new LoyaltyRule(0.001m, 1m, minimumRedeemPoints: 500)
        };
        repo.Tiers.Add(new LoyaltyTier("Bronce", 0, allowsRedemption: false));
        var benefit = repo.AddBenefit("Sticker", 1);

        LoyaltyBenefitClaimResult claim = await new ClaimLoyaltyBenefitUseCase(repo, new CountingUow())
            .ExecuteAsync(new ClaimLoyaltyBenefitRequest(customerId, benefit.Id), TestContext.Current.CancellationToken);

        Assert.Equal(1, claim.PointsSpent);
        Assert.Equal(9, repo.Balance);
    }

    [Fact]
    public async Task ClaimAsync_WithRequiredTier_IneligibleCustomer_Throws()
    {
        var customerId = Guid.NewGuid();
        var repo = new InMemLoyaltyRepository(customerId, balance: 100);
        var goldTier = new LoyaltyTier("Oro", 500);
        repo.Tiers.Add(goldTier);
        var benefit = new LoyaltyBenefit("Descuento VIP", 50, "Solo Oro", goldTier.Id);
        repo.Benefits.Add(benefit);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClaimLoyaltyBenefitUseCase(repo, new CountingUow())
            .ExecuteAsync(new ClaimLoyaltyBenefitRequest(customerId, benefit.Id), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ClaimAsync_WithRequiredTier_EligibleCustomer_Succeeds()
    {
        var customerId = Guid.NewGuid();
        var repo = new InMemLoyaltyRepository(customerId, balance: 600);
        var goldTier = new LoyaltyTier("Oro", 500);
        repo.Tiers.Add(goldTier);
        var benefit = new LoyaltyBenefit("Descuento VIP", 50, "Solo Oro", goldTier.Id);
        repo.Benefits.Add(benefit);

        LoyaltyBenefitClaimResult claim = await new ClaimLoyaltyBenefitUseCase(repo, new CountingUow())
            .ExecuteAsync(new ClaimLoyaltyBenefitRequest(customerId, benefit.Id), TestContext.Current.CancellationToken);

        Assert.Equal(50, claim.PointsSpent);
        Assert.Equal(550, repo.Balance);
    }

    [Fact]
    public async Task DeleteTierAsync_WithAssignedBenefits_Throws()
    {
        var repo = new InMemLoyaltyRepository(Guid.NewGuid(), balance: 0);
        var goldTier = new LoyaltyTier("Oro", 500);
        repo.Tiers.Add(goldTier);
        var benefit = new LoyaltyBenefit("Descuento VIP", 50, "Solo Oro", goldTier.Id);
        repo.Benefits.Add(benefit);

        var useCase = new DeleteLoyaltyTierUseCase(repo, new CountingUow());
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(goldTier.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeliverAsync_MovesThePendingClaimToDelivered_AndRefusesASecondDelivery()
    {
        var customerId = Guid.NewGuid();
        var repo = new InMemLoyaltyRepository(customerId, balance: 2_000);
        var benefit = repo.AddBenefit("Asesoría personalizada", 1_000);
        var uow = new CountingUow();
        LoyaltyBenefitClaimResult claim = await new ClaimLoyaltyBenefitUseCase(repo, uow)
            .ExecuteAsync(new ClaimLoyaltyBenefitRequest(customerId, benefit.Id), TestContext.Current.CancellationToken);

        var deliver = new DeliverLoyaltyBenefitClaimUseCase(repo, uow);
        LoyaltyBenefitClaimResult delivered = await deliver.ExecuteAsync(claim.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(LoyaltyBenefitClaimStatus.Delivered, delivered.Status);
        Assert.NotNull(delivered.DeliveredAt);

        await Assert.ThrowsAsync<InvalidOperationException>(() => deliver.ExecuteAsync(claim.Id, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancelAsync_RestoresExactlyThePointsSpent_AndRefusesASecondCancellation()
    {
        var customerId = Guid.NewGuid();
        var repo = new InMemLoyaltyRepository(customerId, balance: 2_500);
        var benefit = repo.AddBenefit("Asesoría personalizada", 1_000);
        var uow = new CountingUow();
        LoyaltyBenefitClaimResult claim = await new ClaimLoyaltyBenefitUseCase(repo, uow)
            .ExecuteAsync(new ClaimLoyaltyBenefitRequest(customerId, benefit.Id), TestContext.Current.CancellationToken);
        Assert.Equal(1_500, repo.Balance);

        var cancel = new CancelLoyaltyBenefitClaimUseCase(repo, uow);
        LoyaltyBenefitClaimResult cancelled = await cancel.ExecuteAsync(
            claim.Id, "Cliente desistió", Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(LoyaltyBenefitClaimStatus.Cancelled, cancelled.Status);
        Assert.Equal("Cliente desistió", cancelled.CancellationReason);
        Assert.Equal(2_500, repo.Balance);

        LoyaltyTransaction restoration = repo.Transactions.Single(t => t.Type == LoyaltyTransactionType.BenefitRestored);
        Assert.Equal(1_000, restoration.Points);
        Assert.Equal(repo.Transactions.Single(t => t.Type == LoyaltyTransactionType.BenefitRedeemed).Id, restoration.CompensatesTransactionId);

        // Cancelling twice would hand the customer the same points a second time.
        await Assert.ThrowsAsync<InvalidOperationException>(() => cancel.ExecuteAsync(
            claim.Id, "Otra vez", null, TestContext.Current.CancellationToken));
        Assert.Equal(2_500, repo.Balance);
    }

    [Fact]
    public async Task CancelAsync_WorksOnADeliveredClaim_SoAMisrecordedDeliveryCanBeUndone()
    {
        var customerId = Guid.NewGuid();
        var repo = new InMemLoyaltyRepository(customerId, balance: 2_000);
        var benefit = repo.AddBenefit("Asesoría personalizada", 1_000);
        var uow = new CountingUow();
        LoyaltyBenefitClaimResult claim = await new ClaimLoyaltyBenefitUseCase(repo, uow)
            .ExecuteAsync(new ClaimLoyaltyBenefitRequest(customerId, benefit.Id), TestContext.Current.CancellationToken);
        await new DeliverLoyaltyBenefitClaimUseCase(repo, uow).ExecuteAsync(claim.Id, null, TestContext.Current.CancellationToken);

        LoyaltyBenefitClaimResult cancelled = await new CancelLoyaltyBenefitClaimUseCase(repo, uow)
            .ExecuteAsync(claim.Id, "Entrega registrada por error", null, TestContext.Current.CancellationToken);

        Assert.Equal(LoyaltyBenefitClaimStatus.Cancelled, cancelled.Status);
        Assert.Equal(2_000, repo.Balance);
    }

    [Fact]
    public async Task DeleteAsync_DeactivatesAClaimedBenefitInsteadOfRemovingIt()
    {
        var customerId = Guid.NewGuid();
        var repo = new InMemLoyaltyRepository(customerId, balance: 2_000);
        var benefit = repo.AddBenefit("Asesoría personalizada", 1_000);
        var uow = new CountingUow();
        await new ClaimLoyaltyBenefitUseCase(repo, uow)
            .ExecuteAsync(new ClaimLoyaltyBenefitRequest(customerId, benefit.Id), TestContext.Current.CancellationToken);

        bool removed = await new DeleteLoyaltyBenefitUseCase(repo, uow).ExecuteAsync(benefit.Id, TestContext.Current.CancellationToken);

        Assert.False(removed);
        Assert.Single(repo.Benefits);
        Assert.False(repo.Benefits.Single().IsActive);
    }

    [Fact]
    public async Task DeleteAsync_RemovesANeverClaimedBenefit()
    {
        var repo = new InMemLoyaltyRepository(Guid.NewGuid(), balance: 0);
        var benefit = repo.AddBenefit("Mug de regalo", 300);

        bool removed = await new DeleteLoyaltyBenefitUseCase(repo, new CountingUow())
            .ExecuteAsync(benefit.Id, TestContext.Current.CancellationToken);

        Assert.True(removed);
        Assert.Empty(repo.Benefits);
    }

    [Fact]
    public async Task SaveAsync_CreatesThenUpdatesWithoutChangingTheStoredId()
    {
        var repo = new InMemLoyaltyRepository(Guid.NewGuid(), balance: 0);
        var useCase = new SaveLoyaltyBenefitUseCase(repo, new CountingUow());

        LoyaltyBenefitResult created = await useCase.ExecuteAsync(
            new SaveLoyaltyBenefitRequest("Asesoría", 1_000, "Una hora"), TestContext.Current.CancellationToken);

        LoyaltyBenefitResult updated = await useCase.ExecuteAsync(
            new SaveLoyaltyBenefitRequest("Asesoría premium", 1_500, "Dos horas", IsActive: false, BenefitId: created.Id),
            TestContext.Current.CancellationToken);

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Asesoría premium", updated.Name);
        Assert.Equal(1_500, updated.PointsCost);
        Assert.False(updated.IsActive);
        Assert.Single(repo.Benefits);
    }

    [Fact]
    public async Task GetCustomerBenefitClaimsAsync_WithoutAnAccount_ReturnsEmptyRatherThanThrowing()
    {
        var repo = new InMemLoyaltyRepository(Guid.NewGuid(), balance: 0);

        IReadOnlyList<LoyaltyBenefitClaimResult> claims = await new GetCustomerBenefitClaimsUseCase(repo)
            .ExecuteAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Empty(claims);
    }

    private sealed class InMemLoyaltyRepository : ILoyaltyRepository
    {
        private readonly LoyaltyAccount? _account;

        public InMemLoyaltyRepository(Guid customerId, int balance)
        {
            _account = new LoyaltyAccount(customerId);
            Balance = balance;
        }

        public LoyaltyRule? ActiveRule { get; set; }
        public int Balance { get; private set; }
        public List<LoyaltyTier> Tiers { get; } = [];
        public List<LoyaltyTransaction> Transactions { get; } = [];
        public List<LoyaltyBenefit> Benefits { get; } = [];
        public List<LoyaltyBenefitClaim> Claims { get; } = [];

        public LoyaltyBenefit AddBenefit(string name, int pointsCost)
        {
            var benefit = new LoyaltyBenefit(name, pointsCost);
            Benefits.Add(benefit);
            return benefit;
        }

        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
            => Task.FromResult(_account?.CustomerId == customerId ? _account : null);

        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken ct = default) => Task.CompletedTask;

        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken ct = default)
        {
            Transactions.Add(transaction);
            Balance += transaction.Points;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(Transactions.Take(maxCount).ToList());

        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken ct = default) => Task.FromResult(ActiveRule);
        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyRule>>([]);
        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken ct = default) => Task.CompletedTask;

        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken ct = default) => Task.FromResult(Balance);

        public Task<int> GetTotalIssuedPointsAsync(CancellationToken ct = default)
            => Task.FromResult(Transactions.Where(t => t.Points > 0).Sum(t => t.Points));

        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken ct = default) => Task.FromResult(_account is null ? 0 : 1);

        public Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
            => Task.FromResult(Transactions.Count(t => t.Type == LoyaltyTransactionType.Redeemed));

        public Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(Transactions.Take(maxCount).ToList());

        public Task<LoyaltyTier?> GetTierByIdAsync(Guid tierId, CancellationToken ct = default)
            => Task.FromResult(Tiers.FirstOrDefault(t => t.Id == tierId));

        public Task<bool> HasBenefitsRequiringTierAsync(Guid tierId, CancellationToken ct = default)
            => Task.FromResult(Benefits.Any(b => b.RequiredTierId == tierId));

        public Task<IReadOnlyList<LoyaltyTier>> GetTiersAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTier>>(Tiers);

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
            => Task.FromResult(Claims.Any(claim => claim.LoyaltyBenefitId == benefitId));

        public Task AddClaimAsync(LoyaltyBenefitClaim claim, CancellationToken ct = default)
        {
            Claims.Add(claim);
            return Task.CompletedTask;
        }

        public Task UpdateClaimAsync(LoyaltyBenefitClaim claim, CancellationToken ct = default) => Task.CompletedTask;

        public Task<LoyaltyBenefitClaim?> GetClaimByIdAsync(Guid claimId, CancellationToken ct = default)
            => Task.FromResult(Claims.FirstOrDefault(claim => claim.Id == claimId));

        public Task<IReadOnlyList<LoyaltyBenefitClaim>> GetClaimsByAccountAsync(Guid loyaltyAccountId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoyaltyBenefitClaim>>(
                Claims.Where(claim => claim.LoyaltyAccountId == loyaltyAccountId).ToList());

        public Task<LoyaltyTransaction?> GetTransactionByIdAsync(Guid transactionId, CancellationToken ct = default)
            => Task.FromResult(Transactions.FirstOrDefault(transaction => transaction.Id == transactionId));
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
