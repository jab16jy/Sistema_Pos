using POS.Domain.ValueObjects;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Records a single payment applied against a receivable balance.
/// Each instance reduces the parent Receivable's outstanding balance.
/// </summary>
public sealed class ReceivableSettlement
{
    private ReceivableSettlement()
    {
    }

    internal ReceivableSettlement(Guid receivableId, Money amount, PaymentMethod method, string? bankOrPlatform, Guid? cashierUserId = null, Guid? shiftId = null)
    {
        Id = Guid.NewGuid();
        ReceivableId = receivableId;
        Amount = amount;
        Method = method;
        BankOrPlatform = string.IsNullOrWhiteSpace(bankOrPlatform) ? null : bankOrPlatform.Trim();
        AppliedAt = DateTimeOffset.UtcNow;
        CashierUserId = cashierUserId;
        ShiftId = shiftId;
    }

    public Guid Id { get; private set; }
    public Guid ReceivableId { get; private set; }
    public Money Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public string? BankOrPlatform { get; private set; }
    public DateTimeOffset AppliedAt { get; private set; }
    public Guid? CashierUserId { get; private set; }
    public Guid? ShiftId { get; private set; }
}
