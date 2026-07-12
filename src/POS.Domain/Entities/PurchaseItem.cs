using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class PurchaseItem
{
    private PurchaseItem() { }

    public PurchaseItem(
        Guid productId,
        string productName,
        string? productReference,
        string purchaseUnitName,
        decimal unitsPerPurchaseUnit,
        Quantity quantity,
        Money purchaseUnitCost)
    {
        if (productId == Guid.Empty) throw new ArgumentException("Product is required.", nameof(productId));
        if (string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("Product name is required.", nameof(productName));
        if (string.IsNullOrWhiteSpace(purchaseUnitName)) throw new ArgumentException("Purchase unit name is required.", nameof(purchaseUnitName));
        if (unitsPerPurchaseUnit <= 0m) throw new ArgumentOutOfRangeException(nameof(unitsPerPurchaseUnit));
        if (purchaseUnitCost.Amount <= 0m) throw new ArgumentOutOfRangeException(nameof(purchaseUnitCost));

        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = productName.Trim();
        ProductReference = string.IsNullOrWhiteSpace(productReference) ? null : productReference.Trim();
        PurchaseUnitName = purchaseUnitName.Trim();
        UnitsPerPurchaseUnit = unitsPerPurchaseUnit;
        Quantity = quantity.Value;
        InventoryUnitsReceived = quantity.Value * unitsPerPurchaseUnit;
        PurchaseUnitCost = purchaseUnitCost;
        BaseUnitCost = new Money(purchaseUnitCost.Amount / unitsPerPurchaseUnit);
        Subtotal = purchaseUnitCost * quantity;
        // Kept for legacy compatibility; new rows intentionally store the same immutable line subtotal.
        TotalCost = Subtotal.Value.Amount;
    }

    public Guid Id { get; private set; }
    public Guid PurchaseId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal TotalCost { get; private set; }
    public string? ProductName { get; private set; }
    public string? ProductReference { get; private set; }
    public string? PurchaseUnitName { get; private set; }
    public decimal? UnitsPerPurchaseUnit { get; private set; }
    public decimal? InventoryUnitsReceived { get; private set; }
    public Money? PurchaseUnitCost { get; private set; }
    public Money? BaseUnitCost { get; private set; }
    public Money? Subtotal { get; private set; }
}
