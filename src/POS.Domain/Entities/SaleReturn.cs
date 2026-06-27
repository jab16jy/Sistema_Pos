namespace POS.Domain.Entities;

public sealed class SaleReturn
{
    private SaleReturn()
    {
        Reason = string.Empty;
    }

    public SaleReturn(Guid originalSaleId, Guid processedByUserId, string reason)
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

        Id = Guid.NewGuid();
        OriginalSaleId = originalSaleId;
        ProcessedByUserId = processedByUserId;
        ReturnedAt = DateTimeOffset.UtcNow;
        Reason = reason.Trim();
    }

    public Guid Id { get; private set; }
    public Guid OriginalSaleId { get; private set; }
    public Guid ProcessedByUserId { get; private set; }
    public DateTimeOffset ReturnedAt { get; private set; }
    public string Reason { get; private set; }
}
