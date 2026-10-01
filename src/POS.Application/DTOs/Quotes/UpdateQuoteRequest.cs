namespace POS.Application.DTOs.Quotes;

/// <summary>
/// Corrects a quote that is still a proposal. The lines reuse <see cref="CreateQuoteItemRequest"/>
/// so an edit runs through the very same discount ceiling and price resolution as the quote it
/// replaces — there is no cheaper way to guarantee the two agree.
/// </summary>
public sealed record UpdateQuoteRequest(
    Guid QuoteId,
    string CustomerName,
    string? Phone,
    string? Email,
    int ValidityDays,
    string? Observations,
    IReadOnlyCollection<CreateQuoteItemRequest> Items);