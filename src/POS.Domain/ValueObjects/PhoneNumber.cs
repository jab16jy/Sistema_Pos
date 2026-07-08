namespace POS.Domain.ValueObjects;

public readonly record struct PhoneNumber
{
    public string Value { get; }

    public PhoneNumber(string value)
    {
        string trimmed = value?.Trim() ?? string.Empty;
        int digits = trimmed.Count(char.IsDigit);

        bool onlyAllowedChars = trimmed.All(c => char.IsDigit(c) || c is '+' or '-' or ' ' or '(' or ')');
        if (digits < 7 || digits > 15 || !onlyAllowedChars)
        {
            throw new ArgumentException("El número de teléfono no es válido.", nameof(value));
        }

        Value = trimmed;
    }
}
