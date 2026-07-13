using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace POS.Presentation.Converters;

public sealed class ModuleKeyToIconConverter : IValueConverter
{
    private static readonly IReadOnlyDictionary<string, PackIconKind> IconsByKey = new Dictionary<string, PackIconKind>
    {
        ["products"] = PackIconKind.PackageVariant,
        ["inventory"] = PackIconKind.Warehouse,
        ["suppliers"] = PackIconKind.Cart,
        ["reports"] = PackIconKind.ChartBox,
        ["shifts"] = PackIconKind.ClockOutline,
        ["loyalty"] = PackIconKind.StarCircle,
        ["backup"] = PackIconKind.Backup,
        ["users"] = PackIconKind.AccountCog,
        ["audit"] = PackIconKind.ClipboardCheck,
        ["roles"] = PackIconKind.ShieldAccount
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key && IconsByKey.TryGetValue(key, out var icon) ? icon : PackIconKind.Apps;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
