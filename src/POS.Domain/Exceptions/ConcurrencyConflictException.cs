namespace POS.Domain.Exceptions;

public sealed class ConcurrencyConflictException : InvalidOperationException
{
    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
