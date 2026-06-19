using POS.Application.DTOs.Sales;

namespace POS.Application.DTOs.Quotes;

public sealed record ConvertQuoteToSaleRequest(
    Guid QuoteId,
    Guid CashierUserId,
    IReadOnlyCollection<CreateSalePaymentRequest> Payments);
