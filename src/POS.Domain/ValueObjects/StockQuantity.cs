namespace POS.Domain.ValueObjects;

public readonly record struct StockQuantity
{
    public decimal Value { get; }

    public StockQuantity(decimal value)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Stock quantity cannot be negative.");
        }

        Value = value;
    }
}
