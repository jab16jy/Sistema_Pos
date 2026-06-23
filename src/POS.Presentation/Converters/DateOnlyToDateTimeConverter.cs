using System;
using System.Globalization;
using System.Windows.Data;

namespace POS.Presentation.Converters;

public sealed class DateOnlyToDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateOnly d ? new DateTime(d.Year, d.Month, d.Day) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime dt ? DateOnly.FromDateTime(dt) : null;
}
