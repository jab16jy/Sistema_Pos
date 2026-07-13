namespace POS.Domain.Exceptions;

public sealed class TransactionConflictException : Exception
{
    public TransactionConflictException(Exception innerException)
        : base("The transaction conflicted with a concurrent operation.", innerException)
    {
    }
}
