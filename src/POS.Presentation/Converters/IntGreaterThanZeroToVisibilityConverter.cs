using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace POS.Presentation.Converters;

/// <summary>Shows the target when the bound integer is greater than zero (e.g. a loyalty points badge).</summary>
public sealed class IntGreaterThanZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
