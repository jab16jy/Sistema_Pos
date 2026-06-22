using POS.Domain.ValueObjects;

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

    internal ReceivableSettlement(Guid receivableId, Money amount)
    {
        Id = Guid.NewGuid();
        ReceivableId = receivableId;
        Amount = amount;
        AppliedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ReceivableId { get; private set; }
    public Money Amount { get; private set; }
    public DateTimeOffset AppliedAt { get; private set; }
}
