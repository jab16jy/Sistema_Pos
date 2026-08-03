using System;
using System.Globalization;
using System.Windows.Data;

namespace POS.Presentation.Converters;

/// <summary>Accepts "10.8" and "10,8" as the same value — cashiers mix decimal separators.</summary>
public sealed class FlexibleDecimalConverter : IValueConverter
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is decimal d ? d.ToString("0.##", ColombianCulture) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string text = (value as string ?? string.Empty).Trim().Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal result)
            ? result
            : Binding.DoNothing;
    }
}
