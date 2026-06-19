using POS.Domain.Enums;

namespace POS.Application.DTOs.Quotes;

public sealed record QuoteResult(
    Guid QuoteId,
    string QuoteNumber,
    decimal Total,
    string ExpiresAt,
    QuoteStatus Status);
