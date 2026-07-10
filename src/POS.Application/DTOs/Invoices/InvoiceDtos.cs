using POS.Domain.Enums;

namespace POS.Application.DTOs.Invoices;

public sealed record SalesHistoryRequest(
    DateOnly? From,
    DateOnly? To,
    string? SearchText,
    int Page = 1,
    int PageSize = 50);

public sealed record SaleHistoryResult(
    Guid SaleId,
    string InvoiceNumber,
    Guid? CustomerId,
    string? CustomerName,
    SaleStatus Status,
    DateTimeOffset CreatedAt,
    decimal Total,
    string? CashierName = null);

public sealed record InvoiceDetailResult(
    Guid SaleId,
    string InvoiceNumber,
    Guid? CustomerId,
    string? CustomerName,
    SaleStatus Status,
    DateTimeOffset CreatedAt,
    decimal Total,
    string? CashierName,
    IReadOnlyList<InvoiceDetailItemResult> Items,
    IReadOnlyList<InvoicePaymentResult> Payments);

public sealed record InvoiceDetailItemResult(
    Guid SaleItemId,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal Subtotal);

public sealed record InvoicePaymentResult(
    Guid PaymentId,
    POS.Domain.Enums.PaymentMethod Method,
    decimal Amount,
    decimal? ReceivedAmount,
    decimal ChangeAmount,
    string? BankOrPlatform);

/// <summary>
/// A single line in a partial return request.
/// SaleItemId identifies the original sale item; QuantityReturned must be in [1, originalQty].
/// RefundAmount is the monetary amount to refund for this line.
/// </summary>
public sealed record ReturnLineRequest(
    Guid SaleItemId,
    int QuantityReturned,
    decimal RefundAmount);

/// <summary>
/// Request to process a sale return (full or partial).
/// When Items is null or empty, a full return of all items is performed (backward compatible).
/// When Items is provided, only the listed items are returned at the specified quantities.
/// </summary>
public sealed record ProcessReturnRequest(
    Guid SaleId,
    Guid ProcessedByUserId,
    string Reason,
    IReadOnlyList<ReturnLineRequest>? Items = null);

public sealed record CancelSaleRequest(
    Guid SaleId,
    Guid CancelledByUserId,
    string Reason);
