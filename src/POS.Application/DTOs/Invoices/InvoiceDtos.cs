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
    IReadOnlyList<InvoicePaymentResult> Payments,
    decimal OutstandingBalance = 0m,
    IReadOnlyList<InvoiceSettlementResult>? Settlements = null);

public sealed record InvoiceDetailItemResult(
    Guid SaleItemId,
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal Subtotal,
    ProductPresentation Presentation = ProductPresentation.Unit,
    int UnitsPerPresentation = 1)
{
    public string PresentationLabel => Presentation == ProductPresentation.Box ? $"Caja x{UnitsPerPresentation}" : "Unidad";
}

public sealed record InvoicePaymentResult(
    Guid PaymentId,
    POS.Domain.Enums.PaymentMethod Method,
    decimal Amount,
    decimal? ReceivedAmount,
    decimal ChangeAmount,
    string? BankOrPlatform);

public sealed record InvoiceSettlementResult(
    Guid SettlementId,
    POS.Domain.Enums.PaymentMethod Method,
    decimal Amount,
    DateTimeOffset AppliedAt,
    string? BankOrPlatform);

/// <summary>
/// A single line in a partial return request.
/// SaleItemId identifies the original sale item; QuantityReturned must be in [1, originalQty].
/// </summary>
public sealed record ReturnLineRequest(
    Guid SaleItemId,
    int QuantityReturned);

public enum ReturnResolutionChoice
{
    Cash = 1,
    DebitCard = 2,
    CreditCard = 3,
    Transfer = 4,
    Receivable = 5
}

/// <summary>
/// Request to process a sale return (full or partial).
/// The operation id makes retried submissions idempotent; the resolution choice selects settlement or receivable reconciliation.
/// </summary>
public sealed record ProcessReturnRequest(
    Guid SaleId,
    string Reason,
    Guid OperationId,
    ReturnResolutionChoice ResolutionChoice,
    IReadOnlyList<ReturnLineRequest>? Items = null);

public sealed record CancelSaleRequest(
    Guid SaleId,
    Guid CancelledByUserId,
    string Reason);
