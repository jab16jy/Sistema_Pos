namespace POS.Application.DTOs.Quotes;

public sealed record CreateQuoteItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal Discount,
    string? PresentationName = null);
