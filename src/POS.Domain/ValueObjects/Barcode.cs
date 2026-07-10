namespace POS.Domain.ValueObjects;

public readonly record struct Barcode
{
    /// <summary>Minimum length for a printable Code 128 value generated or entered by the POS.</summary>
    public const int MinimumCode128Length = 4;

    /// <summary>Maximum length kept safe for scanners, label printers, and the product column.</summary>
    public const int MaximumCode128Length = 64;

    public string Value { get; }

    public Barcode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Barcode is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length == 13 && normalized.All(char.IsAsciiDigit))
        {
            if (!IsValidEan13(normalized))
            {
                throw new ArgumentException("EAN-13 barcode has an invalid check digit.", nameof(value));
            }
        }
        else if (!IsValidCode128(normalized))
        {
            throw new ArgumentException(
                $"Code 128 barcode must contain {MinimumCode128Length} to {MaximumCode128Length} printable ASCII characters.",
                nameof(value));
        }

        Value = normalized;
    }

    public static bool IsValidEan13(string? value)
    {
        if (value is null || value.Length != 13 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        int sum = 0;
        for (int index = 0; index < 12; index++)
        {
            int digit = value[index] - '0';
            sum += index % 2 == 0 ? digit : digit * 3;
        }

        int checkDigit = (10 - sum % 10) % 10;
        return value[12] - '0' == checkDigit;
    }

    private static bool IsValidCode128(string value) =>
        value.Length is >= MinimumCode128Length and <= MaximumCode128Length &&
        value.All(character => character is >= ' ' and <= '~');
}
