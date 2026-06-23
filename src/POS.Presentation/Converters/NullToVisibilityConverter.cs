using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace POS.Presentation.Converters;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool IsInverse { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value is null;
        bool show = IsInverse ? isNull : !isNull;
        return show ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
