namespace POS.Application.DTOs.Purchases;

public sealed record RegisterPurchaseItemRequest(Guid ProductId, decimal Quantity, decimal TotalCost);

public sealed record RegisterPurchaseRequest(
    Guid SupplierId,
    Guid UserId,
    IReadOnlyList<RegisterPurchaseItemRequest> Items);

public sealed record RegisterPurchaseResult(Guid PurchaseId, Guid SupplierId, decimal TotalCost, int ItemCount);

public sealed record PurchaseDraftItem(Guid ProductId, decimal Quantity, decimal TotalCost);
