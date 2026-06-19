namespace POS.Domain.ValueObjects;

public readonly record struct Email
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("Email is not valid.", nameof(value));
        }

        Value = value.Trim().ToLowerInvariant();
    }
}
