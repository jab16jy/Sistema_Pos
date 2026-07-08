using System.Globalization;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Shared.Cashier;

public sealed class CashierCartItemViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");
    private decimal _quantity;
    private decimal _discount;

    public CashierCartItemViewModel(Guid productId, string productName, string productCode, decimal quantity, decimal unitPrice, decimal discount = 0m)
    {
        ProductId = productId;
        ProductName = productName;
        ProductCode = productCode;
        _quantity = quantity;
        UnitPrice = unitPrice;
        _discount = NormalizeDiscount(discount);
    }

    public Guid ProductId { get; }
    public string ProductName { get; }
    public string ProductCode { get; }

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
    public string DiscountText => $"Dto. {Discount:0.##}%";
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
