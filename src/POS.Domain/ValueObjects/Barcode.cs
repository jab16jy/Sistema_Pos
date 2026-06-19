using System.Text.RegularExpressions;

namespace POS.Domain.ValueObjects;

public readonly partial record struct Barcode
{
    public string Value { get; }

    public Barcode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Barcode is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (!BarcodePattern().IsMatch(normalized))
        {
            throw new ArgumentException("Barcode must contain 4 to 32 alphanumeric characters, dashes, or underscores.", nameof(value));
        }

        Value = normalized;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{4,32}$")]
    private static partial Regex BarcodePattern();
}
