using System.Globalization;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Shared.Cashier;

public sealed class CashierCartItemViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");
    private decimal _quantity;

    public CashierCartItemViewModel(Guid productId, string productName, string productCode, decimal quantity, decimal unitPrice)
    {
        ProductId = productId;
        ProductName = productName;
        ProductCode = productCode;
        _quantity = quantity;
        UnitPrice = unitPrice;
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
    public decimal Subtotal => Quantity * UnitPrice;
    public string UnitPriceText => FormatCurrency(UnitPrice);
    public string SubtotalText => FormatCurrency(Subtotal);

    public void Increase(decimal quantity)
    {
        Quantity += quantity;
    }

    private static string FormatCurrency(decimal amount) => $"${amount.ToString("N0", ColombianCulture)}";
}
