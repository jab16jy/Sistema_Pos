namespace POS.Domain.Enums;

public enum LoyaltyTransactionType
{
    Earned = 1,
    Redeemed = 2,
    EarnReversed = 3,
    RedemptionRestored = 4,

    /// <summary>Points spent claiming a catalogue benefit. Never linked to a sale.</summary>
    BenefitRedeemed = 5,

    /// <summary>Points given back when a benefit claim is cancelled.</summary>
    BenefitRestored = 6
}
