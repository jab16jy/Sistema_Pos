using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Represents one immutable, signed loyalty point movement.
/// </summary>
public sealed class LoyaltyTransaction
{
    private LoyaltyTransaction()
    {
        Description = string.Empty;
    }

    public LoyaltyTransaction(
        Guid loyaltyAccountId,
        LoyaltyTransactionType type,
        int points,
        Guid? saleId,
        string? description = null)
        : this(loyaltyAccountId, type, points, saleId, description, null, null)
    {
        if (type is LoyaltyTransactionType.EarnReversed or LoyaltyTransactionType.RedemptionRestored or LoyaltyTransactionType.BenefitRestored)
            throw new ArgumentException("Compensation transactions must be created from their source transaction.", nameof(type));
    }

    private LoyaltyTransaction(
        Guid loyaltyAccountId,
        LoyaltyTransactionType type,
        int points,
        Guid? saleId,
        string? description,
        Guid? compensatesTransactionId,
        Guid? saleReturnId)
    {
        if (loyaltyAccountId == Guid.Empty)
        {
            throw new ArgumentException("Loyalty account id is required.", nameof(loyaltyAccountId));
        }

        ValidateTypeAndPoints(type, points);

        Id = Guid.NewGuid();
        LoyaltyAccountId = loyaltyAccountId;
        Type = type;
        Points = points;
        SaleId = saleId;
        CompensatesTransactionId = compensatesTransactionId;
        SaleReturnId = saleReturnId;
        Description = description?.Trim() ?? string.Empty;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public static LoyaltyTransaction CreateCompensation(
        LoyaltyTransaction source,
        string reason,
        int? pointsToCompensate = null,
        Guid? saleReturnId = null)
    {
        ArgumentNullException.ThrowIfNull(source);

        var type = source.Type switch
        {
            LoyaltyTransactionType.Earned => LoyaltyTransactionType.EarnReversed,
            LoyaltyTransactionType.Redeemed => LoyaltyTransactionType.RedemptionRestored,
            LoyaltyTransactionType.BenefitRedeemed => LoyaltyTransactionType.BenefitRestored,
            _ => throw new ArgumentException("Only original earned, redeemed, or benefit-redeemed transactions can be compensated.", nameof(source))
        };

        // Sale-driven movements must keep their sale evidence, so the original guard still applies
        // to them. A benefit claim has no sale by design — its evidence is the claim row itself.
        if (type is not LoyaltyTransactionType.BenefitRestored && source.SaleId is null)
            throw new ArgumentException("Only sale-linked transactions can be compensated.", nameof(source));

        int magnitude = pointsToCompensate ?? Math.Abs(source.Points);
        if (magnitude <= 0 || magnitude > Math.Abs(source.Points))
            throw new ArgumentOutOfRangeException(nameof(pointsToCompensate), "Compensation points must be positive and cannot exceed the source amount.");
        if (saleReturnId == Guid.Empty)
            throw new ArgumentException("Sale return id must be non-empty when supplied.", nameof(saleReturnId));

        int points = type == LoyaltyTransactionType.EarnReversed ? -magnitude : magnitude;
        return new LoyaltyTransaction(source.LoyaltyAccountId, type, points, source.SaleId, reason, source.Id, saleReturnId);
    }

    public Guid Id { get; private set; }
    public Guid LoyaltyAccountId { get; private set; }
    public LoyaltyTransactionType Type { get; private set; }

    /// <summary>Signed point amount: positive for Earned, negative for Redeemed.</summary>
    public int Points { get; private set; }

    /// <summary>Reference to the sale that triggered this transaction (nullable for manual adjustments).</summary>
    public Guid? SaleId { get; private set; }

    /// <summary>The immutable source movement corrected by this compensation, when applicable.</summary>
    public Guid? CompensatesTransactionId { get; private set; }

    /// <summary>The return that caused this compensation, when the evidence is available.</summary>
    public Guid? SaleReturnId { get; private set; }

    public string Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private static void ValidateTypeAndPoints(LoyaltyTransactionType type, int points)
    {
        if (points == 0)
            throw new ArgumentException("Loyalty transactions must have non-zero points.", nameof(points));

        bool valid = type switch
        {
            LoyaltyTransactionType.Earned or LoyaltyTransactionType.RedemptionRestored or LoyaltyTransactionType.BenefitRestored => points > 0,
            LoyaltyTransactionType.Redeemed or LoyaltyTransactionType.EarnReversed or LoyaltyTransactionType.BenefitRedeemed => points < 0,
            _ => false
        };

        if (!valid)
            throw new ArgumentException($"Transaction type '{type}' has an invalid point sign.", nameof(points));
    }
}
