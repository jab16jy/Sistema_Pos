namespace POS.Domain.ValueObjects;

/// <summary>
/// Shared input validation for contact fields used by Customer, Supplier and Quote.
/// Optional fields return null when blank; otherwise the value is validated and normalized.
/// </summary>
public static class ContactInfo
{
    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("El nombre es obligatorio.", nameof(value));
        }

        string trimmed = value.Trim();
        if (!trimmed.Any(char.IsLetter))
        {
            throw new ArgumentException("El nombre no es válido.", nameof(value));
        }

        return trimmed;
    }

    public static string? NormalizeEmail(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new Email(value).Value;

    public static string? NormalizePhone(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new PhoneNumber(value).Value;
}
