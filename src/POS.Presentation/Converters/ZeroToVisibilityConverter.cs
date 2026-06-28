using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace POS.Presentation.Converters;

public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is 0 or 0.0 or 0m ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
