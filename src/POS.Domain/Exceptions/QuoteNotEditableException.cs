using POS.Domain.Enums;

namespace POS.Domain.Exceptions;

/// <summary>
/// A quote that already left the proposal stage cannot be edited. Converted is expressed with the
/// same wording as <see cref="QuoteAlreadyConvertedException"/> so both paths read identically to
/// the operator whether the aggregate or the use case caught it first.
/// </summary>
public sealed class QuoteNotEditableException : DomainException
{
    public QuoteNotEditableException(string quoteNumber, QuoteStatus status)
        : base(status switch
        {
            QuoteStatus.Converted => $"La cotización '{quoteNumber}' ya fue convertida a venta y no puede modificarse.",
            QuoteStatus.Cancelled => $"La cotización '{quoteNumber}' está anulada y no puede modificarse.",
            QuoteStatus.Expired => $"La cotización '{quoteNumber}' está vencida y no puede modificarse.",
            _ => $"La cotización '{quoteNumber}' está en estado {status} y no puede modificarse."
        })
    {
    }
}