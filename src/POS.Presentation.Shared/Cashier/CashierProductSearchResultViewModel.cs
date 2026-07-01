using System.Globalization;
using POS.Application.DTOs.Products;

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
    public string StockText => $"Stock: {AvailableStock:0.##}";

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
