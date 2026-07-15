namespace POS.Domain.ValueObjects;

public readonly record struct Email
{
    public string Value { get; }

    public Email(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("El correo electrónico no es válido.", nameof(value));
        }

        Value = value.Trim().ToLowerInvariant();
    }
}
