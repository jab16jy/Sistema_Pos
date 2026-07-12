using POS.Application.DTOs.Products;

namespace POS.Presentation.Shared.ViewModels;

public sealed class SupplierProductAssignmentItem : ViewModelBase
{
    private bool _isSelected;

    public SupplierProductAssignmentItem(ProductLookupResult product)
    {
        Product = product;
    }

    public ProductLookupResult Product { get; }

    public Guid ProductId => Product.ProductId;

    public string Name => Product.Name;

    public string Barcode => Product.Barcode;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
