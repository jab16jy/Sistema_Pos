using System.Globalization;
using POS.Domain.Enums;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Shared.Cashier;

public sealed class CashierCartItemViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");
    private decimal _quantity;
    private decimal _discount;

    public CashierCartItemViewModel(
        Guid productId,
        string productName,
        string productCode,
        decimal quantity,
        decimal unitPrice,
        decimal discount = 0m,
        string? presentationName = null,
        int unitsPerLine = 1,
        decimal taxRate = 0m)
    {
        TaxRate = taxRate;
        ProductId = productId;
        ProductName = productName;
        ProductCode = productCode;
        _quantity = quantity;
        UnitPrice = unitPrice;
        _discount = NormalizeDiscount(discount);
        PresentationName = string.IsNullOrWhiteSpace(presentationName) ? null : presentationName.Trim();
        UnitsPerLine = unitsPerLine < 1 ? 1 : unitsPerLine;
    }

    public Guid ProductId { get; }
    public string ProductName { get; }
    public string ProductCode { get; }

    /// <summary>Name of presentation sold, or null for base unit.</summary>
    public string? PresentationName { get; }

    /// <summary>Base inventory units consumed per presentation.</summary>
    public int UnitsPerLine { get; }

    /// <summary>Base inventory units this line consumes in total.</summary>
    public decimal InventoryUnits => Quantity * UnitsPerLine;

    public string PresentationLabel => Domain.Enums.ProductPresentationLabel.For(PresentationName, UnitsPerLine);

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            // Presentations carry the fractions (half box, quarter box); quantity is always whole.
            decimal normalized = Math.Max(decimal.Truncate(value), 1m);
            if (SetProperty(ref _quantity, normalized))
            {
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(SubtotalText));
                OnPropertyChanged(nameof(InventoryUnits));
            }
        }
    }

    public decimal UnitPrice { get; }

    /// <summary>Product VAT rate as a percentage. Prices are tax-inclusive, so this splits the
    /// line between taxable base and VAT rather than adding anything on top.</summary>
    public decimal TaxRate { get; }
    public decimal Discount
    {
        get => _discount;
        set
        {
            if (SetProperty(ref _discount, NormalizeDiscount(value)))
            {
                OnPropertyChanged(nameof(DiscountText));
                OnPropertyChanged(nameof(HasDiscount));
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(SubtotalText));
            }
        }
    }

    public bool HasDiscount => Discount > 0m;
    public string DiscountText => $"Dto. {Discount.ToString("0.##", ColombianCulture)}%";
    public decimal Subtotal => Quantity * UnitPrice * (1m - (Discount / 100m));
    public string UnitPriceText => FormatCurrency(UnitPrice);
    public string SubtotalText => FormatCurrency(Subtotal);

    public void Increase(decimal quantity)
    {
        Quantity += quantity;
    }

    private static string FormatCurrency(decimal amount) => $"${amount.ToString("N0", ColombianCulture)}";
    private static decimal NormalizeDiscount(decimal value) => decimal.Round(Math.Clamp(value, 0m, 100m), 2, MidpointRounding.AwayFromZero);
}
