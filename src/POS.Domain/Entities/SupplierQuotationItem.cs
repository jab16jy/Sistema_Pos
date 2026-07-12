using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class SupplierQuotationItem
{
    private SupplierQuotationItem() { }

    public SupplierQuotationItem(Guid productId, string productName, string? productReference, string purchaseUnitName, decimal unitsPerPurchaseUnit, Quantity quantity, Money purchaseUnitCost)
    {
        if (productId == Guid.Empty) throw new ArgumentException("Product is required.", nameof(productId));
        if (string.IsNullOrWhiteSpace(productName) || string.IsNullOrWhiteSpace(purchaseUnitName)) throw new ArgumentException("Product and purchase unit are required.");
        if (unitsPerPurchaseUnit <= 0m || purchaseUnitCost.Amount <= 0m) throw new ArgumentOutOfRangeException(nameof(unitsPerPurchaseUnit));
        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = productName.Trim();
        ProductReference = string.IsNullOrWhiteSpace(productReference) ? null : productReference.Trim();
        PurchaseUnitName = purchaseUnitName.Trim();
        UnitsPerPurchaseUnit = unitsPerPurchaseUnit;
        Quantity = quantity.Value;
        PurchaseUnitCost = purchaseUnitCost;
    }

    public Guid Id { get; private set; }
    public Guid SupplierQuotationId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string? ProductReference { get; private set; }
    public string PurchaseUnitName { get; private set; } = string.Empty;
    public decimal UnitsPerPurchaseUnit { get; private set; }
    public decimal Quantity { get; private set; }
    public Money PurchaseUnitCost { get; private set; }
}
