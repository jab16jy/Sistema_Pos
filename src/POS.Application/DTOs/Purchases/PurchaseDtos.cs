namespace POS.Application.DTOs.Purchases;

public sealed record RegisterPurchaseItemRequest(Guid ProductId, decimal Quantity)
{
    [Obsolete("Purchase costs are sourced from the active supplier catalog.")]
    public RegisterPurchaseItemRequest(Guid productId, decimal quantity, decimal ignoredTotalCost) : this(productId, quantity) { }
}

public sealed record RegisterPurchaseRequest(
    Guid SupplierId,
    Guid UserId,
    DateOnly EffectiveDate,
    string? InvoiceNumber,
    string? PaymentTerms,
    string PaymentMethod,
    string? Observations,
    decimal VatRate,
    IReadOnlyList<RegisterPurchaseItemRequest> Items,
    Guid? SupplierQuotationId = null)
{
    [Obsolete("Use the auditable purchase-header overload.")]
    public RegisterPurchaseRequest(Guid supplierId, Guid userId, IReadOnlyList<RegisterPurchaseItemRequest> items)
        : this(supplierId, userId, DateOnly.FromDateTime(DateTime.Today), null, null, "Contado", null, 19m, items) { }
}

public sealed record RegisterPurchaseResult(Guid PurchaseId, Guid SupplierId, decimal TotalCost, int ItemCount, decimal VatAmount = 0m);

public sealed record SupplierCatalogItemResult(
    Guid SupplierId,
    Guid ProductId,
    string ProductName,
    string? ProductReference,
    string? SupplierSku,
    string PurchaseUnitName,
    decimal UnitsPerPurchaseUnit,
    decimal PurchaseUnitCost,
    bool IsActive);

public sealed record SaveSupplierCatalogItemRequest(
    Guid SupplierId,
    Guid ProductId,
    string? SupplierSku,
    string PurchaseUnitName,
    decimal UnitsPerPurchaseUnit,
    decimal PurchaseUnitCost,
    bool IsActive);

public sealed record PurchaseDraftItem(
    Guid ProductId,
    string ProductName,
    string? ProductReference,
    string PurchaseUnitName,
    decimal UnitsPerPurchaseUnit,
    decimal PurchaseUnitCost,
    decimal Quantity)
{
    public decimal InventoryUnitsReceived => Quantity * UnitsPerPurchaseUnit;
    public decimal Subtotal => decimal.Round(Quantity * PurchaseUnitCost, 2, MidpointRounding.AwayFromZero);
}

public sealed record PurchaseHistoryRequest(Guid? SupplierId, DateOnly? FromEffectiveDate, DateOnly? ToEffectiveDate, string? InvoiceNumber, int Page = 1, int PageSize = 50);
public sealed record PurchaseHistoryLineResult(Guid ProductId, string? ProductName, string? ProductReference, string? PurchaseUnitName, decimal Quantity, decimal? UnitsPerPurchaseUnit, decimal? InventoryUnitsReceived, decimal? PurchaseUnitCost, decimal? BaseUnitCost, decimal? Subtotal, decimal LegacyTotalCost);
public sealed record PurchaseHistoryResult(Guid Id, Guid SupplierId, string SupplierName, DateOnly? EffectiveDate, string? InvoiceNumber, string? PaymentTerms, string? PaymentMethod, string? Observations, decimal? Subtotal, decimal? VatRate, decimal? VatAmount, decimal? Total, IReadOnlyList<PurchaseHistoryLineResult> Items);
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
