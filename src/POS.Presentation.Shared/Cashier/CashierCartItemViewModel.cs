using System.Globalization;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Shared.Cashier;

public sealed class CashierCartItemViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");
    private decimal _quantity;

    public CashierCartItemViewModel(Guid productId, string productName, decimal quantity, decimal unitPrice)
    {
        ProductId = productId;
        ProductName = productName;
        _quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Guid ProductId { get; }
    public string ProductName { get; }

    public decimal Quantity
    {
        get => _quantity;
        private set
        {
            if (SetProperty(ref _quantity, value))
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
