using System.Globalization;
using POS.Application.DTOs.Products;


namespace POS.Presentation.Shared.Cashier;

public sealed class CashierProductSearchResultViewModel
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    public CashierProductSearchResultViewModel(ProductLookupResult product)
    {
        Product = product ?? throw new ArgumentNullException(nameof(product));
        var list = new List<PresentationOptionViewModel>
        {
            new PresentationOptionViewModel(null, 1, product.UnitPrice) { Product = this }
        };
        if (product.Presentations is { Count: > 0 })
        {
            // Unit-type presentations (Units == 1) first, then box-type ones biggest-to-smallest.
            foreach (var p in product.Presentations.OrderBy(p => p.Units == 1 ? 0 : 1).ThenByDescending(p => p.Units))
            {
                list.Add(new PresentationOptionViewModel(p.Name, p.Units, p.Price) { Product = this });
            }
        }
        AvailablePresentations = list;
    }

    public ProductLookupResult Product { get; }
    public Guid ProductId => Product.ProductId;
    public string Name => Product.Name;
    public string Barcode => Product.Barcode;
    public decimal UnitPrice => Product.UnitPrice;
    public decimal AvailableStock => Product.AvailableStock;
    public decimal MinimumStock => Product.MinimumStock;
    public decimal TaxRate => Product.TaxRate;
    public string UnitPriceText => FormatCurrency(UnitPrice);
    public string StockText => $"Stock: {AvailableStock.ToString("0.##", ColombianCulture)}";

    // ── Packaged presentations ──
    public IReadOnlyList<PresentationOptionViewModel> AvailablePresentations { get; }

    /// <summary>Units and price for the chosen presentation name (null/blank = Unidad).</summary>
    public (int Units, decimal Price)? PackageFor(string? presentationName)
    {
        if (string.IsNullOrWhiteSpace(presentationName))
        {
            return (1, UnitPrice);
        }

        var opt = AvailablePresentations.FirstOrDefault(p => string.Equals(p.Name, presentationName.Trim(), StringComparison.OrdinalIgnoreCase));
        return opt is null ? null : (opt.Units, opt.Price);
    }

    public bool Supports(string? presentationName) => PackageFor(presentationName) is not null;

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
