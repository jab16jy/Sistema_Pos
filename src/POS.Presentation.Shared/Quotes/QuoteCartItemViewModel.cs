using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Shared.Quotes;

public sealed class QuoteCartItemViewModel : ViewModelBase
{
    private decimal _quantity;
    private decimal _discount;

    public QuoteCartItemViewModel(Guid productId, string productName, decimal unitPrice)
    {
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        _quantity = 1m;
        _discount = 0m;
    }

    public Guid ProductId { get; }
    public string ProductName { get; }
    public decimal UnitPrice { get; }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(Subtotal));
            }
        }
    }

    public decimal Discount
    {
        get => _discount;
        set
        {
            if (SetProperty(ref _discount, value))
            {
                OnPropertyChanged(nameof(Subtotal));
            }
        }
    }

    public decimal Subtotal => UnitPrice * Quantity * (1m - Discount / 100m);
}
