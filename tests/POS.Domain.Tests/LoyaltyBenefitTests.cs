using POS.Domain.Entities;
using POS.Domain.Enums;
using Xunit;

namespace POS.Domain.Tests;

public sealed class LoyaltyBenefitTests
{
    // --- LoyaltyBenefit catalogue ---

    [Fact]
    public void LoyaltyBenefit_IsCreatedActiveWithTrimmedText()
    {
        var benefit = new LoyaltyBenefit("  Asesoría personalizada  ", 1_000, "  Una hora con un asesor  ");

        Assert.Equal("Asesoría personalizada", benefit.Name);
        Assert.Equal("Una hora con un asesor", benefit.Description);
        Assert.Equal(1_000, benefit.PointsCost);
        Assert.True(benefit.IsActive);
    }

    [Fact]
    public void LoyaltyBenefit_WithoutDescription_ExposesEmptyStringNotNull()
    {
        var benefit = new LoyaltyBenefit("Asesoría", 1_000);

        Assert.Equal(string.Empty, benefit.Description);
    }

    [Theory]
    [InlineData("", 1_000)]
    [InlineData("   ", 1_000)]
    public void LoyaltyBenefit_RejectsMissingName(string name, int pointsCost)
    {
        Assert.Throws<ArgumentException>(() => new LoyaltyBenefit(name, pointsCost));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void LoyaltyBenefit_RejectsNonPositiveCost(int pointsCost)
    {
        // A zero-cost benefit would be free points-wise and would produce a zero-point movement,
        // which the ledger rejects anyway.
        Assert.Throws<ArgumentOutOfRangeException>(() => new LoyaltyBenefit("Asesoría", pointsCost));
    }

    [Fact]
    public void LoyaltyBenefit_RejectsTextBeyondTheStoredColumnLengths()
    {
        Assert.Throws<ArgumentException>(() => new LoyaltyBenefit(new string('x', 101), 1_000));
        Assert.Throws<ArgumentException>(() => new LoyaltyBenefit("Asesoría", 1_000, new string('x', 501)));
    }

    [Fact]
    public void LoyaltyBenefit_DeactivateAndActivate_FlipTheCatalogueFlag()
    {
        var benefit = new LoyaltyBenefit("Asesoría", 1_000);

        benefit.Deactivate();
        Assert.False(benefit.IsActive);

        benefit.Activate();
        Assert.True(benefit.IsActive);
    }

    [Fact]
    public void LoyaltyBenefit_Update_DoesNotResetTheActiveFlag()
    {
        var benefit = new LoyaltyBenefit("Asesoría", 1_000);
        benefit.Deactivate();

        benefit.Update("Asesoría premium", 1_500);

        Assert.Equal("Asesoría premium", benefit.Name);
        Assert.Equal(1_500, benefit.PointsCost);
        Assert.False(benefit.IsActive);
    }

    // --- LoyaltyBenefitClaim state machine ---

    private static LoyaltyBenefitClaim NewClaim(int pointsSpent = 1_000)
        => new(Guid.NewGuid(), Guid.NewGuid(), "Asesoría personalizada", pointsSpent, Guid.NewGuid());

    [Fact]
    public void LoyaltyBenefitClaim_StartsPendingWithSnapshottedNameAndCost()
    {
        var claim = NewClaim();

        Assert.Equal(LoyaltyBenefitClaimStatus.Pending, claim.Status);
        Assert.Equal("Asesoría personalizada", claim.BenefitName);
        Assert.Equal(1_000, claim.PointsSpent);
        Assert.Null(claim.DeliveredAt);
        Assert.Null(claim.CancelledAt);
    }

    [Fact]
    public void LoyaltyBenefitClaim_MarkDelivered_RecordsWhoAndWhen()
    {
        var claim = NewClaim();
        var userId = Guid.NewGuid();

        claim.MarkDelivered(userId);

        Assert.Equal(LoyaltyBenefitClaimStatus.Delivered, claim.Status);
        Assert.Equal(userId, claim.DeliveredByUserId);
        Assert.NotNull(claim.DeliveredAt);
    }

    [Fact]
    public void LoyaltyBenefitClaim_MarkDelivered_Twice_Throws()
    {
        var claim = NewClaim();
        claim.MarkDelivered();

        Assert.Throws<InvalidOperationException>(() => claim.MarkDelivered());
    }

    [Fact]
    public void LoyaltyBenefitClaim_MarkDelivered_AfterCancel_Throws()
    {
        var claim = NewClaim();
        claim.Cancel("Cliente desistió");

        Assert.Throws<InvalidOperationException>(() => claim.MarkDelivered());
    }

    [Fact]
    public void LoyaltyBenefitClaim_CanBeCancelledFromDelivered_SoAnAdminCanUndoAMisrecordedDelivery()
    {
        var claim = NewClaim();
        claim.MarkDelivered();

        claim.Cancel("Entrega registrada por error", Guid.NewGuid());

        Assert.Equal(LoyaltyBenefitClaimStatus.Cancelled, claim.Status);
        Assert.NotNull(claim.CancelledAt);
    }

    [Fact]
    public void LoyaltyBenefitClaim_Cancel_Twice_Throws()
    {
        // This is the only thing stopping the same points from being restored twice, since the
        // compensation index does not cover benefit restorations (they carry no SaleReturnId).
        var claim = NewClaim();
        claim.Cancel("Primera anulación");

        Assert.Throws<InvalidOperationException>(() => claim.Cancel("Segunda anulación"));
    }

    [Fact]
    public void LoyaltyBenefitClaim_Cancel_WithoutReason_StoresNullNotEmpty()
    {
        var claim = NewClaim();

        claim.Cancel("   ");

        Assert.Null(claim.CancellationReason);
    }

    [Fact]
    public void LoyaltyBenefitClaim_Cancel_RejectsReasonBeyondTheStoredColumnLength()
    {
        var claim = NewClaim();

        Assert.Throws<ArgumentException>(() => claim.Cancel(new string('x', 301)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void LoyaltyBenefitClaim_RejectsNonPositivePointsSpent(int pointsSpent)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NewClaim(pointsSpent));
    }

    [Fact]
    public void LoyaltyBenefitClaim_RequiresBenefitAccountAndRedeemTransaction()
    {
        Assert.Throws<ArgumentException>(() => new LoyaltyBenefitClaim(Guid.Empty, Guid.NewGuid(), "Asesoría", 1_000, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new LoyaltyBenefitClaim(Guid.NewGuid(), Guid.Empty, "Asesoría", 1_000, Guid.NewGuid()));
        Assert.Throws<ArgumentException>(() => new LoyaltyBenefitClaim(Guid.NewGuid(), Guid.NewGuid(), "Asesoría", 1_000, Guid.Empty));
    }

    // --- Ledger movements for benefit claims ---

    [Fact]
    public void BenefitRedeemed_RequiresNegativePoints_AndNeedsNoSale()
    {
        var accountId = Guid.NewGuid();

        var movement = new LoyaltyTransaction(accountId, LoyaltyTransactionType.BenefitRedeemed, -1_000, null, "Asesoría");

        Assert.Equal(-1_000, movement.Points);
        Assert.Null(movement.SaleId);
        Assert.Throws<ArgumentException>(() =>
            new LoyaltyTransaction(accountId, LoyaltyTransactionType.BenefitRedeemed, 1_000, null));
    }

    [Fact]
    public void BenefitRestored_CannotBeConstructedDirectly()
    {
        // Like the other compensation types, it must come from its source movement so the restored
        // amount can never exceed what was spent.
        Assert.Throws<ArgumentException>(() =>
            new LoyaltyTransaction(Guid.NewGuid(), LoyaltyTransactionType.BenefitRestored, 1_000, null));
    }

    [Fact]
    public void CreateCompensation_OnBenefitRedeemed_RestoresTheFullAmountWithoutASale()
    {
        var source = new LoyaltyTransaction(Guid.NewGuid(), LoyaltyTransactionType.BenefitRedeemed, -1_000, null, "Asesoría");

        LoyaltyTransaction compensation = LoyaltyTransaction.CreateCompensation(source, "Canje anulado");

        Assert.Equal(LoyaltyTransactionType.BenefitRestored, compensation.Type);
        Assert.Equal(1_000, compensation.Points);
        Assert.Equal(source.Id, compensation.CompensatesTransactionId);
        Assert.Null(compensation.SaleId);
    }

    [Fact]
    public void CreateCompensation_OnBenefitRedeemed_CannotRestoreMoreThanWasSpent()
    {
        var source = new LoyaltyTransaction(Guid.NewGuid(), LoyaltyTransactionType.BenefitRedeemed, -1_000, null);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LoyaltyTransaction.CreateCompensation(source, "Canje anulado", pointsToCompensate: 1_001));
    }

    [Theory]
    [InlineData(LoyaltyTransactionType.Earned, 10)]
    [InlineData(LoyaltyTransactionType.Redeemed, -10)]
    public void CreateCompensation_StillRequiresASale_ForSaleDrivenMovements(LoyaltyTransactionType type, int points)
    {
        // Regression guard: relaxing the sale requirement for benefit claims must not relax it for
        // the earn/redeem movements that a sale return compensates.
        var source = new LoyaltyTransaction(Guid.NewGuid(), type, points, null);

        Assert.Throws<ArgumentException>(() => LoyaltyTransaction.CreateCompensation(source, "Sin venta"));
    }
}
