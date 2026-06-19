using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class CashClosing
{
    private CashClosing()
    {
    }

    public CashClosing(Guid userId, Money expectedCash, Money countedCash, Money digitalPayments)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        ExpectedCash = expectedCash;
        CountedCash = countedCash;
        DigitalPayments = digitalPayments;
        ClosedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Money ExpectedCash { get; private set; }
    public Money CountedCash { get; private set; }
    public Money DigitalPayments { get; private set; }
    public DateTimeOffset ClosedAt { get; private set; }

    public decimal Difference => CountedCash.Amount - ExpectedCash.Amount;
}
