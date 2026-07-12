using POS.Domain.Enums;

namespace POS.Application.DTOs.Quotes;

public sealed record QuoteDetailResult(
    Guid QuoteId,
    string QuoteNumber,
    Guid? CustomerId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    QuoteStatus Status,
    string ExpiresAt,
    decimal Total,
    IReadOnlyList<QuoteDetailItemResult> Items);

public sealed record QuoteDetailItemResult(
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal Subtotal);
