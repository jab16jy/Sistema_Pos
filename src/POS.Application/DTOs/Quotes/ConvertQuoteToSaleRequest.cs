using POS.Application.DTOs.Sales;

namespace POS.Application.DTOs.Quotes;

public sealed record ConvertQuoteToSaleRequest(
    Guid QuoteId,
    IReadOnlyCollection<CreateSalePaymentRequest> Payments);
