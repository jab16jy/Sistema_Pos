using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class CashClosing
{
    private CashClosing()
    {
    }

    /// <summary>
    /// Legacy constructor — kept for backwards compatibility.
    /// New code should use the full separated-totals constructor.
    /// </summary>
    public CashClosing(Guid userId, Money expectedCash, Money countedCash, Money digitalPayments)
        : this(userId, expectedCash, expectedCash, digitalPayments, Money.Zero, countedCash)
    {
        // expectedCash stored in both BilledTotal and CashCollected for legacy callers
    }

    /// <summary>
    /// Full constructor with separated auditable totals.
    /// </summary>
    public CashClosing(
        Guid userId,
        Money billedTotal,
        Money cashCollected,
        Money digitalCollected,
        Money receivableSettlementTotal,
        Money countedCash)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        BilledTotal = billedTotal;
        CashCollected = cashCollected;
        DigitalCollected = digitalCollected;
        ReceivableSettlementTotal = receivableSettlementTotal;
        CountedCash = countedCash;
        ClosedAt = DateTimeOffset.UtcNow;

        // Preserve legacy computed fields for backwards compatibility
        ExpectedCash = new Money(cashCollected.Amount + receivableSettlementTotal.Amount);
        DigitalPayments = digitalCollected;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    // ── Legacy fields (preserved for existing queries / EF mappings) ──
    public Money ExpectedCash { get; private set; }
    public Money CountedCash { get; private set; }
    public Money DigitalPayments { get; private set; }

    // ── Separated auditable totals (RF-06 requirement) ──

    /// <summary>Total sale amounts billed in the period.</summary>
    public Money BilledTotal { get; private set; }

    /// <summary>Cash payments collected from sales.</summary>
    public Money CashCollected { get; private set; }

    /// <summary>Digital payments collected from sales.</summary>
    public Money DigitalCollected { get; private set; }

    /// <summary>Receivable settlement amounts collected in the period.</summary>
    public Money ReceivableSettlementTotal { get; private set; }

    public DateTimeOffset ClosedAt { get; private set; }

    /// <summary>
    /// Difference between counted cash and expected drawer cash.
    /// Positive means surplus; negative means shortage.
    /// ExpectedCash = cash collected from sales + settlement cash collected.
    /// </summary>
    public decimal Difference => CountedCash.Amount - ExpectedCash.Amount;
}
