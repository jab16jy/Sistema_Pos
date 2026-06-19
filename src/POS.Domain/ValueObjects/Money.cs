namespace POS.Domain.ValueObjects;

public readonly record struct Money
{
    public static Money Zero => new(0m);

    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Money cannot be negative.");
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money operator -(Money left, Money right)
    {
        if (left.Amount < right.Amount)
        {
            throw new InvalidOperationException("Money result cannot be negative.");
        }

        return new Money(left.Amount - right.Amount);
    }

    public static Money operator *(Money money, Quantity quantity) => new(money.Amount * quantity.Value);
}
