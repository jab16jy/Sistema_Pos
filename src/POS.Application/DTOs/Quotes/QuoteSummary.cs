using POS.Domain.Enums;

namespace POS.Application.DTOs.Quotes;

public sealed record QuoteSummary(
    Guid QuoteId,
    string QuoteNumber,
    string CustomerName,
    decimal Total,
    QuoteStatus Status,
    string ExpiresAt);
