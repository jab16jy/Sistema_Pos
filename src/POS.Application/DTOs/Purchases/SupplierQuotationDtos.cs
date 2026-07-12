namespace POS.Application.DTOs.Purchases;

public sealed record CreateSupplierQuotationItemRequest(Guid ProductId, decimal Quantity);
public sealed record CreateSupplierQuotationRequest(Guid SupplierId, DateOnly EffectiveDate, string? PaymentTerms, string? Observations, IReadOnlyList<CreateSupplierQuotationItemRequest> Items);
public sealed record SupplierQuotationResult(Guid Id, Guid SupplierId, DateOnly EffectiveDate, string? PaymentTerms, string? Observations, string Status, IReadOnlyList<PurchaseDraftItem> Items);
public sealed record PurchaseDraftFromQuotationResult(Guid SupplierQuotationId, Guid SupplierId, DateOnly EffectiveDate, string? PaymentTerms, string? Observations, IReadOnlyList<PurchaseDraftItem> Items);
