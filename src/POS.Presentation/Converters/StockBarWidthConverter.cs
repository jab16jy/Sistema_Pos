using System.Globalization;
using System.Windows.Data;

namespace POS.Presentation.Converters;

/// <summary>
/// Converts (CurrentStock, MinimumStock) into a pixel width for a stock-level bar.
/// ConverterParameter is the maximum pixel width (default 50).
/// </summary>
internal sealed class StockBarWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return 0.0;

        if (!decimal.TryParse(System.Convert.ToString(values[0], culture), NumberStyles.Any, culture, out var current))
            return 0.0;
        if (!decimal.TryParse(System.Convert.ToString(values[1], culture), NumberStyles.Any, culture, out var minimum))
            return 0.0;

        if (!double.TryParse(parameter as string ?? "50", NumberStyles.Any, culture, out var maxWidth))
            maxWidth = 50.0;

        if (minimum <= 0 && current <= 0) return 0.0;

        var reference = Math.Max(current * 1.5m, minimum * 2m);
        if (reference <= 0) return maxWidth;

        var ratio = (double)(current / reference);
        return Math.Max(0, Math.Min(ratio * maxWidth, maxWidth));
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
