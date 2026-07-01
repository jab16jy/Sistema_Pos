using System.Globalization;
using System.Windows.Data;

namespace POS.Presentation.Converters;

/// <summary>
/// Converts a 0-100 percentage value into a pixel width for a proportional bar.
/// ConverterParameter is the maximum pixel width (default 100).
/// Used by the Reportes view for the Top-10 best-sellers bars and the payment-method
/// breakdown bars (task 13.4/13.5).
/// </summary>
internal sealed class PercentToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (!double.TryParse(System.Convert.ToString(value, culture), NumberStyles.Any, culture, out var percent))
            return 0.0;

        if (!double.TryParse(parameter as string ?? "100", NumberStyles.Any, culture, out var maxWidth))
            maxWidth = 100.0;

        var width = percent / 100.0 * maxWidth;
        return Math.Max(0, Math.Min(width, maxWidth));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
