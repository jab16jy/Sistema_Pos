using MaterialDesignThemes.Wpf;

namespace POS.Presentation.Controls;

internal static class IconMap
{
    public static readonly IReadOnlyDictionary<string, PackIconKind> WireframeToPackIcon = new Dictionary<string, PackIconKind>
    {
        ["point_of_sale"] = PackIconKind.CashRegister,
        ["person"] = PackIconKind.Account,
        ["request_quote"] = PackIconKind.FileDocumentEdit,
        ["receipt_long"] = PackIconKind.Receipt,
        ["admin_panel_settings"] = PackIconKind.ShieldAccount,
        ["inventory_2"] = PackIconKind.PackageVariant,
        ["warehouse"] = PackIconKind.Warehouse,
        ["shopping_cart"] = PackIconKind.Cart,
        ["analytics"] = PackIconKind.ChartBox,
        ["schedule"] = PackIconKind.ClockOutline,
        ["loyalty"] = PackIconKind.StarCircle,
        ["qr_code_2"] = PackIconKind.BarcodeScan
    };
}
