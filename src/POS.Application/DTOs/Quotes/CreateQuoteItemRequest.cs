using POS.Domain.Enums;

namespace POS.Application.DTOs.Quotes;

public sealed record CreateQuoteItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal Discount,
    ProductPresentation Presentation = ProductPresentation.Unit);
