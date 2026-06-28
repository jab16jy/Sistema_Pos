namespace POS.Domain.Entities;

/// <summary>
/// Loyalty account for a customer. One account per customer.
/// Balance is computed from the sum of LoyaltyTransaction.Points — no stored balance column.
/// </summary>
public sealed class LoyaltyAccount
{
    private LoyaltyAccount()
    {
    }

    public LoyaltyAccount(Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        Id = Guid.NewGuid();
        CustomerId = customerId;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    /// <summary>Foreign key to the Customer. Unique — one account per customer.</summary>
    public Guid CustomerId { get; private set; }

    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
