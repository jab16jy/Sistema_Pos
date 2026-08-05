namespace POS.Application.DTOs.Quotes;

public sealed record CustomerPayableQuote(
    Guid QuoteId,
    string QuoteNumber,
    string CustomerName,
    decimal Total,
    DateTime ExpiresAt,
    IReadOnlyList<CustomerPayableQuoteItem> Items);

public sealed record CustomerPayableQuoteItem(
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    decimal Subtotal,
    decimal TaxRate = 0m);
