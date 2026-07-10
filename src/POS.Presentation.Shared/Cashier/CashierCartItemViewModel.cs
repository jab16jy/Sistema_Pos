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
        ProductPresentation presentation = ProductPresentation.Unit,
        int unitsPerLine = 1)
    {
        ProductId = productId;
        ProductName = productName;
        ProductCode = productCode;
        _quantity = quantity;
        UnitPrice = unitPrice;
        _discount = NormalizeDiscount(discount);
        Presentation = presentation;
        UnitsPerLine = unitsPerLine < 1 ? 1 : unitsPerLine;
    }

    public Guid ProductId { get; }
    public string ProductName { get; }
    public string ProductCode { get; }

    /// <summary>Whether this line is sold by unit or by box.</summary>
    public ProductPresentation Presentation { get; }

    /// <summary>Base inventory units consumed per line quantity (1 for a unit line, UnitsPerBox for a box line).</summary>
    public int UnitsPerLine { get; }

    public bool IsBox => Presentation == ProductPresentation.Box;

    /// <summary>Base inventory units this line consumes in total.</summary>
    public decimal InventoryUnits => Quantity * UnitsPerLine;

    public string PresentationLabel => IsBox ? $"Caja x{UnitsPerLine}" : "Unidad";

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            decimal normalized = value < 1m ? 1m : decimal.Round(value, 2, MidpointRounding.AwayFromZero);
            if (SetProperty(ref _quantity, normalized))
            {
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(SubtotalText));
                OnPropertyChanged(nameof(InventoryUnits));
            }
        }
    }

    public decimal UnitPrice { get; }
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
    private static decimal NormalizeDiscount(decimal value) => value < 0m ? 0m : decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
