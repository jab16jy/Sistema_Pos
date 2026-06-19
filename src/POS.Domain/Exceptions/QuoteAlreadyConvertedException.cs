namespace POS.Domain.Exceptions;

public sealed class QuoteAlreadyConvertedException : DomainException
{
    public QuoteAlreadyConvertedException(string quoteNumber)
        : base($"La cotización '{quoteNumber}' ya fue convertida a venta y no puede modificarse.")
    {
    }
}
