namespace POS.Domain.Entities;

public sealed class ProductSupplier
{
    private ProductSupplier()
    {
    }

    public ProductSupplier(Guid productId, Guid supplierId)
    {
        if (productId == Guid.Empty) throw new ArgumentException("Product is required.", nameof(productId));
        if (supplierId == Guid.Empty) throw new ArgumentException("Supplier is required.", nameof(supplierId));
        ProductId = productId;
        SupplierId = supplierId;
        IsActive = false;
    }

    public Guid ProductId { get; private set; }
    public Guid SupplierId { get; private set; }
    public string? SupplierSku { get; private set; }
    public string? PurchaseUnitName { get; private set; }
    public decimal? UnitsPerPurchaseUnit { get; private set; }
    public decimal? PurchaseUnitCost { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsConfigured => !string.IsNullOrWhiteSpace(PurchaseUnitName)
        && UnitsPerPurchaseUnit is > 0m
        && PurchaseUnitCost is > 0m;
    public bool IsEligibleForPurchase => IsActive && IsConfigured;

    public ProductSupplier(Guid productId, Guid supplierId, string? supplierSku, string purchaseUnitName, decimal unitsPerPurchaseUnit, decimal purchaseUnitCost, bool isActive = true)
        : this(productId, supplierId)
    {
        Update(supplierSku, purchaseUnitName, unitsPerPurchaseUnit, purchaseUnitCost, isActive);
    }

    public void Update(string? supplierSku, string purchaseUnitName, decimal unitsPerPurchaseUnit, decimal purchaseUnitCost, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(purchaseUnitName)) throw new ArgumentException("Purchase unit is required.", nameof(purchaseUnitName));
        if (unitsPerPurchaseUnit <= 0m) throw new ArgumentOutOfRangeException(nameof(unitsPerPurchaseUnit), "Conversion factor must be greater than zero.");
        if (purchaseUnitCost <= 0m) throw new ArgumentOutOfRangeException(nameof(purchaseUnitCost), "Purchase unit cost must be greater than zero.");

        SupplierSku = string.IsNullOrWhiteSpace(supplierSku) ? null : supplierSku.Trim();
        PurchaseUnitName = purchaseUnitName.Trim();
        UnitsPerPurchaseUnit = unitsPerPurchaseUnit;
        PurchaseUnitCost = decimal.Round(purchaseUnitCost, 2, MidpointRounding.AwayFromZero);
        IsActive = isActive;
    }

    public void Deactivate() => IsActive = false;
}
