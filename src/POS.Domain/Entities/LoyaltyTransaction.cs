using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Represents a single loyalty point event (earn or redeem).
/// Points are signed: positive for Earned, negative for Redeemed.
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
    {
        if (loyaltyAccountId == Guid.Empty)
        {
            throw new ArgumentException("Loyalty account id is required.", nameof(loyaltyAccountId));
        }

        if (type == LoyaltyTransactionType.Earned && points <= 0)
        {
            throw new ArgumentException("Earned transactions must have positive points.", nameof(points));
        }

        if (type == LoyaltyTransactionType.Redeemed && points >= 0)
        {
            throw new ArgumentException("Redeemed transactions must have negative points.", nameof(points));
        }

        Id = Guid.NewGuid();
        LoyaltyAccountId = loyaltyAccountId;
        Type = type;
        Points = points;
        SaleId = saleId;
        Description = description?.Trim() ?? string.Empty;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid LoyaltyAccountId { get; private set; }
    public LoyaltyTransactionType Type { get; private set; }

    /// <summary>Signed point amount: positive for Earned, negative for Redeemed.</summary>
    public int Points { get; private set; }

    /// <summary>Reference to the sale that triggered this transaction (nullable for manual adjustments).</summary>
    public Guid? SaleId { get; private set; }

    public string Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
