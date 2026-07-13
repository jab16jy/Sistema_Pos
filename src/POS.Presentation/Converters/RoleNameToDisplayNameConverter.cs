using System;
using System.Globalization;
using System.Windows.Data;

namespace POS.Presentation.Converters;

/// <summary>Wraps <see cref="RoleDisplayNames"/> for single-value XAML bindings (e.g. ComboBox item templates).</summary>
public sealed class RoleNameToDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => RoleDisplayNames.ToDisplayName(value as string);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
