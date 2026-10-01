using POS.Domain.Enums;

namespace POS.Domain.Exceptions;

/// <summary>
/// A quote that already left the proposal stage cannot be edited. Never thrown for
/// <see cref="QuoteStatus.Converted"/> — that raises <see cref="QuoteAlreadyConvertedException"/>, which
/// owns the wording. Both call sites construct this only after ruling Converted out, so the status is
/// one of Draft, Active, Cancelled or Expired, and the switch below is exhaustive by construction.
/// </summary>
public sealed class QuoteNotEditableException : DomainException
{
    public QuoteNotEditableException(string quoteNumber, QuoteStatus status)
        : base(status switch
        {
            QuoteStatus.Cancelled => $"La cotización '{quoteNumber}' está anulada y no puede modificarse.",
            // Everything else reaching here is a proposal whose deadline passed, and it is reported as
            // expired rather than as "estado Draft": that is what it is from the client's point of view,
            // and reporting the raw status would tell the operator the quote is editable right before the
            // write is rejected. Quote.Update/EnsureEditableAt make that substitution before constructing
            // this, so this arm cannot silently start describing a non-expired quote.
            _ => $"La cotización '{quoteNumber}' está vencida y no puede modificarse."
        })
    {
    }
}