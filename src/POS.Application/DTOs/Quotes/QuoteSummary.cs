using POS.Domain.Enums;

namespace POS.Application.DTOs.Quotes;

public sealed record QuoteSummary(
    Guid QuoteId,
    string QuoteNumber,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    decimal Total,
    QuoteStatus Status,
    string ExpiresAt,
    Guid? CustomerId = null,
    bool HasSuccessfulEmail = false,
    string? ReminderLabel = null);
