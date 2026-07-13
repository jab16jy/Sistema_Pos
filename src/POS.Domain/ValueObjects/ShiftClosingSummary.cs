namespace POS.Domain.ValueObjects;

/// <summary>Immutable sales totals captured when a cashier shift is closed.</summary>
public readonly record struct ShiftClosingSummary(
    Money SalesTotal,
    Money CashPayments,
    Money DigitalPayments)
{
    public static ShiftClosingSummary Empty => new(Money.Zero, Money.Zero, Money.Zero);
}
