using System.Globalization;
using POS.Application.DTOs.Products;
using POS.Domain.Enums;

namespace POS.Presentation.Shared.Cashier;

public sealed class CashierProductSearchResultViewModel
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    public CashierProductSearchResultViewModel(ProductLookupResult product)
    {
        Product = product ?? throw new ArgumentNullException(nameof(product));
    }

    public ProductLookupResult Product { get; }
    public Guid ProductId => Product.ProductId;
    public string Name => Product.Name;
    public string Barcode => Product.Barcode;
    public decimal UnitPrice => Product.UnitPrice;
    public decimal AvailableStock => Product.AvailableStock;
    public decimal MinimumStock => Product.MinimumStock;
    public string UnitPriceText => FormatCurrency(UnitPrice);
    public string StockText => $"Stock: {AvailableStock.ToString("0.##", ColombianCulture)}";

    // ── Packaged presentations (each optional, each priced on its own in the catalogue) ──
    public int? UnitsPerBox => Product.UnitsPerBox;
    public decimal? BoxPrice => Product.BoxPrice;
    public bool SupportsBox => IsConfigured(UnitsPerBox, BoxPrice);
    public string BoxPriceText => BoxPrice is decimal price ? FormatCurrency(price) : string.Empty;
    public string BoxLabel => DescribePackage(SupportsBox, UnitsPerBox, BoxPrice);

    public int? HalfBoxUnits => Product.HalfBoxUnits;
    public decimal? HalfBoxPrice => Product.HalfBoxPrice;
    public bool SupportsHalfBox => IsConfigured(HalfBoxUnits, HalfBoxPrice);
    public string HalfBoxLabel => DescribePackage(SupportsHalfBox, HalfBoxUnits, HalfBoxPrice);

    public int? QuarterBoxUnits => Product.QuarterBoxUnits;
    public decimal? QuarterBoxPrice => Product.QuarterBoxPrice;
    public bool SupportsQuarterBox => IsConfigured(QuarterBoxUnits, QuarterBoxPrice);
    public string QuarterBoxLabel => DescribePackage(SupportsQuarterBox, QuarterBoxUnits, QuarterBoxPrice);

    /// <summary>Units and price for the chosen presentation, or null when the product does not offer it.</summary>
    public (int Units, decimal Price)? PackageFor(ProductPresentation presentation) => presentation switch
    {
        ProductPresentation.Box when SupportsBox => (UnitsPerBox!.Value, BoxPrice!.Value),
        ProductPresentation.HalfBox when SupportsHalfBox => (HalfBoxUnits!.Value, HalfBoxPrice!.Value),
        ProductPresentation.QuarterBox when SupportsQuarterBox => (QuarterBoxUnits!.Value, QuarterBoxPrice!.Value),
        _ => null
    };

    public bool Supports(ProductPresentation presentation) =>
        presentation == ProductPresentation.Unit || PackageFor(presentation) is not null;

    private static bool IsConfigured(int? units, decimal? price) => units is > 0 && price is not null;

    private static string DescribePackage(bool supported, int? units, decimal? price) =>
        supported ? $"x{units} · {FormatCurrency(price!.Value)}" : string.Empty;

    public string StockStatusKind
    {
        get
        {
            if (AvailableStock <= 0m)
            {
                return "error";
            }

            return MinimumStock > 0m && AvailableStock <= MinimumStock ? "warning" : "success";
        }
    }

    public string StockStatusLabel => StockStatusKind switch
    {
        "error" => "Sin stock",
        "warning" => "Stock bajo",
        _ => "En stock"
    };

    private static string FormatCurrency(decimal amount) => $"${amount.ToString("N0", ColombianCulture)}";
}
