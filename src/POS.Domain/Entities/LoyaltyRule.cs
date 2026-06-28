using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

/// <summary>
/// Configurable loyalty rule. Only one rule may be active at a time.
/// Default: 1 point per 1,000 COP (PointsPerCurrencyUnit = 0.001).
/// </summary>
public sealed class LoyaltyRule
{
    private LoyaltyRule()
    {
        Description = string.Empty;
    }

    public LoyaltyRule(decimal pointsPerCurrencyUnit, decimal redemptionValuePerPoint, int minimumRedeemPoints, string? description = null)
    {
        if (pointsPerCurrencyUnit <= 0m)
        {
            throw new ArgumentException("Points per currency unit must be greater than zero.", nameof(pointsPerCurrencyUnit));
        }

        if (redemptionValuePerPoint <= 0m)
        {
            throw new ArgumentException("Redemption value per point must be greater than zero.", nameof(redemptionValuePerPoint));
        }

        if (minimumRedeemPoints < 0)
        {
            throw new ArgumentException("Minimum redeem points cannot be negative.", nameof(minimumRedeemPoints));
        }

        Id = Guid.NewGuid();
        PointsPerCurrencyUnit = pointsPerCurrencyUnit;
        RedemptionValuePerPoint = redemptionValuePerPoint;
        MinimumRedeemPoints = minimumRedeemPoints;
        Description = description?.Trim() ?? string.Empty;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    /// <summary>Points earned per unit of currency (e.g., 0.001 = 1 pt per 1,000 COP).</summary>
    public decimal PointsPerCurrencyUnit { get; private set; }

    /// <summary>COP value of each redeemed point (e.g., 1 = 1 COP per point).</summary>
    public decimal RedemptionValuePerPoint { get; private set; }

    /// <summary>Minimum number of points required to trigger a redemption.</summary>
    public int MinimumRedeemPoints { get; private set; }

    public string Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void Deactivate() => IsActive = false;

    /// <summary>
    /// Calculates points earned for a given sale amount using FLOOR rounding.
    /// Returns 0 if amount is insufficient to earn any points.
    /// </summary>
    public int PointsFor(Money amount)
    {
        var raw = amount.Amount * PointsPerCurrencyUnit;
        return (int)Math.Floor(raw);
    }

    /// <summary>
    /// Calculates the COP discount value for a given number of points.
    /// </summary>
    public Money DiscountFor(int points)
    {
        if (points < 0)
        {
            throw new ArgumentException("Points must be non-negative.", nameof(points));
        }

        return new Money(points * RedemptionValuePerPoint);
    }
}
