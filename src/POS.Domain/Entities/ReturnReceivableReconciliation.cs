using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class ReturnReceivableReconciliation
{
    private ReturnReceivableReconciliation()
    {
    }

    public ReturnReceivableReconciliation(Guid saleReturnId, Guid receivableId, Money amount)
    {
        if (saleReturnId == Guid.Empty)
        {
            throw new ArgumentException("Sale return id is required.", nameof(saleReturnId));
        }

        if (receivableId == Guid.Empty)
        {
            throw new ArgumentException("Receivable id is required.", nameof(receivableId));
        }

        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Reconciliation amount must be greater than zero.");
        }

        Id = Guid.NewGuid();
        SaleReturnId = saleReturnId;
        ReceivableId = receivableId;
        Amount = amount;
        ReconciledAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid SaleReturnId { get; private set; }
    public Guid ReceivableId { get; private set; }
    public Money Amount { get; private set; }
    public DateTimeOffset ReconciledAt { get; private set; }
}
