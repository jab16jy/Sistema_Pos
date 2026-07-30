using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// One customer claiming one catalogue benefit. Points leave the account the moment the claim is
/// created; the claim then tracks whether the business actually delivered what was paid for.
/// </summary>
public sealed class LoyaltyBenefitClaim
{
    private LoyaltyBenefitClaim()
    {
        BenefitName = string.Empty;
    }

    public LoyaltyBenefitClaim(
        Guid loyaltyBenefitId,
        Guid loyaltyAccountId,
        string benefitName,
        int pointsSpent,
        Guid redeemTransactionId,
        Guid? claimedByUserId = null)
    {
        if (loyaltyBenefitId == Guid.Empty)
        {
            throw new ArgumentException("Loyalty benefit id is required.", nameof(loyaltyBenefitId));
        }

        if (loyaltyAccountId == Guid.Empty)
        {
            throw new ArgumentException("Loyalty account id is required.", nameof(loyaltyAccountId));
        }

        if (redeemTransactionId == Guid.Empty)
        {
            throw new ArgumentException("Redeem transaction id is required.", nameof(redeemTransactionId));
        }

        if (string.IsNullOrWhiteSpace(benefitName))
        {
            throw new ArgumentException("Benefit name is required.", nameof(benefitName));
        }

        if (pointsSpent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointsSpent), "Points spent must be greater than zero.");
        }

        Id = Guid.NewGuid();
        LoyaltyBenefitId = loyaltyBenefitId;
        LoyaltyAccountId = loyaltyAccountId;
        BenefitName = benefitName.Trim();
        PointsSpent = pointsSpent;
        RedeemTransactionId = redeemTransactionId;
        ClaimedByUserId = claimedByUserId;
        Status = LoyaltyBenefitClaimStatus.Pending;
        ClaimedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Maximum stored length of the free-text cancellation reason.</summary>
    private const int MaxCancellationReasonLength = 300;

    public Guid Id { get; private set; }
    public Guid LoyaltyBenefitId { get; private set; }
    public Guid LoyaltyAccountId { get; private set; }

    /// <summary>
    /// Name and cost are snapshotted at claim time on purpose: repricing "Asesoría" from 1.000 to
    /// 1.500 points must not rewrite what this customer already paid.
    /// </summary>
    public string BenefitName { get; private set; } = string.Empty;

    public int PointsSpent { get; private set; }

    /// <summary>
    /// The BenefitRedeemed movement that took the points out. Cancellation compensates from it, so
    /// the restored amount always matches what was actually spent.
    /// </summary>
    public Guid RedeemTransactionId { get; private set; }

    public LoyaltyBenefitClaimStatus Status { get; private set; }
    public DateTimeOffset ClaimedAt { get; private set; }
    public Guid? ClaimedByUserId { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public Guid? DeliveredByUserId { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public string? CancellationReason { get; private set; }

    public void MarkDelivered(Guid? deliveredByUserId = null)
    {
        if (Status is not LoyaltyBenefitClaimStatus.Pending)
        {
            throw new InvalidOperationException($"Only a pending claim can be delivered. Current status: {Status}.");
        }

        Status = LoyaltyBenefitClaimStatus.Delivered;
        DeliveredAt = DateTimeOffset.UtcNow;
        DeliveredByUserId = deliveredByUserId;
    }

    /// <summary>
    /// Cancels the claim so its points can be restored. Allowed from Delivered as well, so an admin
    /// can undo a delivery that was recorded by mistake.
    /// </summary>
    public void Cancel(string? reason = null, Guid? cancelledByUserId = null)
    {
        if (Status is LoyaltyBenefitClaimStatus.Cancelled)
        {
            throw new InvalidOperationException("This claim is already cancelled.");
        }

        string? trimmedReason = reason?.Trim();
        if (trimmedReason?.Length > MaxCancellationReasonLength)
        {
            throw new ArgumentException($"Cancellation reason cannot exceed {MaxCancellationReasonLength} characters.", nameof(reason));
        }

        Status = LoyaltyBenefitClaimStatus.Cancelled;
        CancelledAt = DateTimeOffset.UtcNow;
        CancelledByUserId = cancelledByUserId;
        CancellationReason = string.IsNullOrEmpty(trimmedReason) ? null : trimmedReason;
    }
}
