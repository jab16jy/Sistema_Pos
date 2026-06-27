using POS.Domain.Enums;

namespace POS.Application.DTOs.Inventory;

public sealed record StockLevelResult(
    Guid ProductId,
    string ProductName,
    string? Barcode,
    decimal CurrentStock,
    decimal MinimumStock,
    bool IsLowStock,
    decimal Deficit);

public sealed record LowStockProductResult(
    Guid ProductId,
    string ProductName,
    string? Barcode,
    decimal CurrentStock,
    decimal MinimumStock,
    decimal Deficit);

public sealed record InventoryMovementResult(
    Guid Id,
    Guid ProductId,
    InventoryMovementType Type,
    decimal Quantity,
    string Reason,
    Guid UserId,
    Guid? ReferenceId,
    DateTimeOffset CreatedAt);

public sealed record AdjustStockRequest(
    Guid ProductId,
    decimal Delta,
    string Reason,
    Guid UserId);
