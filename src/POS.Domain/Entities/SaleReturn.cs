using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class SaleReturn
{
    private readonly List<SaleReturnItem> _items = [];

    private SaleReturn()
    {
        Reason = string.Empty;
    }

    public SaleReturn(Guid originalSaleId, Guid processedByUserId, string reason, Guid? operationId = null)
    {
        if (originalSaleId == Guid.Empty)
        {
            throw new ArgumentException("Original sale id is required.", nameof(originalSaleId));
        }

        if (processedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Processed by user id is required.", nameof(processedByUserId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Return reason is required.", nameof(reason));
        }

        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Operation id cannot be empty.", nameof(operationId));
        }

        Id = Guid.NewGuid();
        OriginalSaleId = originalSaleId;
        ProcessedByUserId = processedByUserId;
        ReturnedAt = DateTimeOffset.UtcNow;
        Reason = reason.Trim();
        OperationId = operationId;
    }

    public Guid Id { get; private set; }
    public Guid OriginalSaleId { get; private set; }
    public Guid ProcessedByUserId { get; private set; }
    public DateTimeOffset ReturnedAt { get; private set; }
    public string Reason { get; private set; }
    public Guid? OperationId { get; private set; }

    /// <summary>Items included in this return. Empty means a full-return (all items from the original sale).</summary>
    public IReadOnlyCollection<SaleReturnItem> Items => _items.AsReadOnly();

    /// <summary>Adds a return line to this return event. The item's SaleReturnId must match this return's Id.</summary>
    public void AddItem(SaleReturnItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.SaleReturnId != Id)
        {
            throw new InvalidOperationException("Return item does not belong to this sale return.");
        }

        _items.Add(item);
    }

    /// <summary>Sum of all return item refund amounts. Returns zero for full returns with no explicit items.</summary>
    public Money TotalRefund()
    {
        var total = _items.Sum(item => item.RefundAmount.Amount);
        return new Money(total);
    }
}
