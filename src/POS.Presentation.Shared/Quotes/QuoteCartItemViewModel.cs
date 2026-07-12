using POS.Domain.Enums;
using POS.Presentation.Shared.ViewModels;

namespace POS.Presentation.Shared.Quotes;

public sealed class QuoteCartItemViewModel : ViewModelBase
{
    private decimal _quantity;
    private decimal _discount;

    public QuoteCartItemViewModel(
        Guid productId,
        string productName,
        decimal unitPrice,
        decimal availableStock = 1000m,
        ProductPresentation presentation = ProductPresentation.Unit,
        int unitsPerPresentation = 1)
    {
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        AvailableStock = availableStock;
        Presentation = presentation;
        UnitsPerPresentation = presentation == ProductPresentation.Unit ? 1 : Math.Max(2, unitsPerPresentation);
        _quantity = 1m;
        _discount = 0m;
    }

    public Guid ProductId { get; }
    public string ProductName { get; }
    public decimal UnitPrice { get; }
    public decimal AvailableStock { get; }
    public ProductPresentation Presentation { get; }
    public int UnitsPerPresentation { get; }
    public string PresentationLabel => Presentation == ProductPresentation.Box ? $"Caja x{UnitsPerPresentation}" : "Unidad";
    public decimal InventoryUnits => Quantity * UnitsPerPresentation;

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(IsOutOfStock));
                OnPropertyChanged(nameof(InventoryUnits));
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

    public bool IsOutOfStock => InventoryUnits > AvailableStock;
}
