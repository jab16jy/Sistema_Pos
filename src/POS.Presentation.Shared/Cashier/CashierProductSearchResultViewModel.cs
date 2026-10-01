using System.Globalization;
using POS.Application.DTOs.Products;
using POS.Presentation.Shared.ViewModels;


namespace POS.Presentation.Shared.Cashier;

public sealed class CashierProductSearchResultViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");
    private PresentationOptionViewModel _selectedPresentation = null!;

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
        SelectedPresentation = list[0];
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

    /// <summary>
    /// The presentation this row will add, defaulting to the loose unit.
    /// </summary>
    /// <remarks>
    /// This is what replaced a wall of one button per presentation in the quotes view: the shop can
    /// configure up to <c>BusinessSettings.MaximumProductPresentations</c> (50) of them, and buttons
    /// wrapped into more and more rows grew the result row without bound, so a single product could
    /// push the rest of the list off the screen. A dropdown keeps the row one line tall whatever the
    /// count, and mirrors the selection model the till already uses.
    /// </remarks>
    public PresentationOptionViewModel SelectedPresentation
    {
        get => _selectedPresentation;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _selectedPresentation, value))
            {
                // PresentationOptionViewModel.IsSelected is what the till's radio group binds, so keep
                // the two selections consistent instead of letting them drift.
                foreach (PresentationOptionViewModel option in AvailablePresentations)
                {
                    option.IsSelected = ReferenceEquals(option, value);
                }
            }
        }
    }

    /// <summary>Price of the selected presentation, so a row can show what adding it will actually cost.</summary>
    public decimal SelectedPresentationPrice => SelectedPresentation.Price;

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
