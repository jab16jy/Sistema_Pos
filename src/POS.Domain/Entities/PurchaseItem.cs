using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class PurchaseItem
{
    private PurchaseItem()
    {
    }

    public PurchaseItem(Guid productId, Quantity quantity, Money totalCost)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        Quantity = quantity.Value;
        TotalCost = totalCost;
    }

    public Guid Id { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public Money TotalCost { get; private set; }
}
