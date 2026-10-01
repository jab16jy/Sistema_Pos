namespace POS.Application.DTOs.Quotes;

/// <summary>
/// Corrects a quote that is still a proposal. The lines reuse <see cref="CreateQuoteItemRequest"/>
/// so an edit runs through the very same discount ceiling and price resolution as the quote it
/// replaces — there is no cheaper way to guarantee the two agree.
/// </summary>
/// <param name="CustomerId">
/// The registered customer to link, or null to leave the quote walk-in. It is a real assignment, not a
/// hint: the customer picker stays live while editing, so without this the operator could pick a
/// customer, save, and be told the quote was updated while it kept pointing at the old one. It may also
/// differ from the current association, because re-linking a quote to the right customer is one of the
/// corrections an edit exists for. Nullable matches the column: the FK is
/// <c>OnDelete(DeleteBehavior.Restrict)</c> and a quote may legitimately name no registered customer.
/// </param>
public sealed record UpdateQuoteRequest(
    Guid QuoteId,
    string CustomerName,
    Guid? CustomerId,
    string? Phone,
    string? Email,
    int ValidityDays,
    string? Observations,
    IReadOnlyCollection<CreateQuoteItemRequest> Items);