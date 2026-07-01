using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace POS.Presentation.Converters;

/// <summary>Reduces a full name to up to two uppercase initials (e.g. "María López" -> "ML") for avatar badges.</summary>
public sealed class InitialsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? text = value as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            return "N";
        }

        string initials = new(text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(word => char.ToUpper(word[0], culture))
            .ToArray());

        return initials.Length == 0 ? "N" : initials;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
