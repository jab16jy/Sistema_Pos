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
    string? ReminderLabel = null,
    /// <summary>
    /// True when the quote's deadline has passed on the Colombian calendar while it is still a
    /// proposal. Carried as its own field rather than inferred from <see cref="ReminderLabel"/> so the
    /// row's edit/convert affordances and its renewal guidance never depend on whether an email was
    /// ever sent.
    /// </summary>
    bool IsExpiredByCalendar = false);
