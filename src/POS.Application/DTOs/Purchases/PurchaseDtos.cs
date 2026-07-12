namespace POS.Application.DTOs.Purchases;

public sealed record RegisterPurchaseItemRequest(Guid ProductId, decimal Quantity, decimal? PurchaseUnitCostOverride = null);

public sealed record RegisterPurchaseRequest(
    Guid SupplierId,
    Guid UserId,
    DateOnly EffectiveDate,
    string? InvoiceNumber,
    string? PaymentTerms,
    string PaymentMethod,
    string? Observations,
    decimal VatRate,
    IReadOnlyList<RegisterPurchaseItemRequest> Items)
{
    [Obsolete("Use the auditable purchase-header overload.")]
    public RegisterPurchaseRequest(Guid supplierId, Guid userId, IReadOnlyList<RegisterPurchaseItemRequest> items)
        : this(supplierId, userId, DateOnly.FromDateTime(DateTime.Today), null, null, "Contado", null, 19m, items) { }
}

public sealed record RegisterPurchaseResult(Guid PurchaseId, Guid SupplierId, decimal TotalCost, int ItemCount, decimal VatAmount = 0m);

public sealed record SupplierCatalogItemResult(Guid SupplierId, string SupplierName, Guid ProductId, string ProductName, string? ProductReference, string? SupplierSku, string? PurchaseUnitName, decimal? UnitsPerPurchaseUnit, decimal? PurchaseUnitCost, bool IsActive)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(PurchaseUnitName) && UnitsPerPurchaseUnit is > 0m && PurchaseUnitCost is > 0m;
    public bool IsEligibleForPurchase => IsActive && IsConfigured;
    public string Status => IsEligibleForPurchase ? "Activo" : IsConfigured ? "Inactivo" : "Requiere configuración";
    public string DisplayLabel => $"{ProductName} — {SupplierSku ?? ProductReference ?? "Sin SKU"} — {PurchaseUnitName ?? "Sin unidad"} — {(PurchaseUnitCost ?? 0m):N2}";
}

public sealed record SaveSupplierCatalogItemRequest(Guid SupplierId, Guid ProductId, string? SupplierSku, string PurchaseUnitName, decimal UnitsPerPurchaseUnit, decimal PurchaseUnitCost, bool IsActive);

public sealed record PurchaseDraftItem(Guid ProductId, string ProductName, string? ProductReference, string PurchaseUnitName, decimal UnitsPerPurchaseUnit, decimal PurchaseUnitCost, decimal Quantity, decimal? PurchaseUnitCostOverride = null)
{
    public PurchaseDraftItem(Guid productId, string productName, decimal quantity, decimal totalCost)
        : this(productId, productName, null, "Unidad", 1m, totalCost / quantity, quantity) { }
    public decimal EffectivePurchaseUnitCost => PurchaseUnitCostOverride ?? PurchaseUnitCost;
    public decimal InventoryUnitsReceived => Quantity * UnitsPerPurchaseUnit;
    public decimal Subtotal => decimal.Round(Quantity * EffectivePurchaseUnitCost, 2, MidpointRounding.AwayFromZero);
    public decimal TotalCost => Subtotal;
}

public sealed record PurchaseHistoryRequest(Guid? SupplierId, DateOnly? FromEffectiveDate, DateOnly? ToEffectiveDate, string? InvoiceNumber, int Page = 1, int PageSize = 50);
public sealed record PurchaseHistoryLineResult(Guid ProductId, string? ProductName, string? ProductReference, string? PurchaseUnitName, decimal Quantity, decimal? UnitsPerPurchaseUnit, decimal? InventoryUnitsReceived, decimal? PurchaseUnitCost, decimal? BaseUnitCost, decimal? Subtotal, decimal LegacyTotalCost);
public sealed record PurchaseHistoryResult(Guid Id, Guid SupplierId, string SupplierName, DateOnly? EffectiveDate, string? InvoiceNumber, string? PaymentTerms, string? PaymentMethod, string? Observations, decimal? Subtotal, decimal? VatRate, decimal? VatAmount, decimal? Total, IReadOnlyList<PurchaseHistoryLineResult> Items);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
