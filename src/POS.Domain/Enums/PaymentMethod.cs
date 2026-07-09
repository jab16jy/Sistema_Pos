namespace POS.Domain.Enums;

public enum PaymentMethod
{
    Cash = 1,
    DebitCard = 2,
    CreditCard = 3,
    Transfer = 4,

    /// <summary>Loyalty points redeemed as a tender covering part of the sale total (not real money).</summary>
    LoyaltyPoints = 5
}
