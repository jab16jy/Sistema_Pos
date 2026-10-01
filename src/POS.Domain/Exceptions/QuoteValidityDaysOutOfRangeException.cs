using POS.Domain.Entities;

namespace POS.Domain.Exceptions;

/// <summary>
/// A quote validity window outside the range the domain accepts. Thrown from the aggregate for both
/// creation and editing, so the two entry points cannot disagree about what a legal window is. See
/// <see cref="Quote.MinimumValidityDays"/> / <see cref="Quote.MaximumValidityDays"/> for the bounds and
/// why they exist; the short version is that an absurd window is refused here with a message naming the
/// rule, instead of being persisted and rendered as a deadline years in the future.
/// </summary>
public sealed class QuoteValidityDaysOutOfRangeException : DomainException
{
    public QuoteValidityDaysOutOfRangeException(string quoteNumber, int validityDays)
        : base($"La vigencia de la cotización '{quoteNumber}' debe estar entre {Quote.MinimumValidityDays} y {Quote.MaximumValidityDays} días. Indicó {validityDays}.")
    {
    }
}
