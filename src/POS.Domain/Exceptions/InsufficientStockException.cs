namespace POS.Domain.Exceptions;

public sealed class InsufficientStockException : DomainException
{
    public InsufficientStockException(string productName, decimal available, decimal requested)
        : base($"Only {available} units are available for '{productName}'. Requested: {requested}.")
    {
    }
}
