using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Products;
using POS.Application.UseCases.Categories;
using POS.Application.UseCases.Products;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class ProductsViewModel : ViewModelBase
{
    private readonly CreateProductUseCase _createProduct;
    private readonly UpdateProductUseCase _updateProduct;
    private readonly DeleteProductUseCase _deleteProduct;
    private readonly SearchProductsUseCase _searchProducts;
    private readonly SearchCategoriesUseCase? _searchCategories;
    private ProductSearchResult? _selectedProduct;
    private string _searchText = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private decimal _costPrice;
    private decimal _salePrice;
    private decimal _currentStock;
    private decimal _minimumStock;
    private string? _barcode;
    private Guid _categoryId;
    private string _message = "Busca productos por nombre o código de barras.";

    public ProductsViewModel(
        CreateProductUseCase createProduct,
        UpdateProductUseCase updateProduct,
        DeleteProductUseCase deleteProduct,
        SearchProductsUseCase searchProducts)
        : this(createProduct, updateProduct, deleteProduct, searchProducts, null)
    {
    }

    public ProductsViewModel(
        CreateProductUseCase createProduct,
        UpdateProductUseCase updateProduct,
        DeleteProductUseCase deleteProduct,
        SearchProductsUseCase searchProducts,
        SearchCategoriesUseCase? searchCategories)
    {
        _createProduct = createProduct;
        _updateProduct = updateProduct;
        _deleteProduct = deleteProduct;
        _searchProducts = searchProducts;
        _searchCategories = searchCategories;
        Categories = [];
        LoadCommand = new RelayCommand(_ => _ = LoadAsync());
        SaveCommand = new RelayCommand(_ => _ = SaveAsync());
        DeleteCommand = new RelayCommand(_ => _ = DeleteAsync(), _ => SelectedProduct is not null);
        ClearCommand = new RelayCommand(_ => ClearForm());
    }

    public ObservableCollection<ProductSearchResult> Products { get; } = [];

    public ObservableCollection<CategoryResult> Categories { get; }

    /// <summary>
    /// Optional sub-ViewModel for category CRUD operations.
    /// Set by App.xaml.cs when the category use cases are available.
    /// </summary>
    public CategoriesViewModel? CategoriesEditor { get; init; }

    public ICommand LoadCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand ClearCommand { get; }

    public ProductSearchResult? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                if (value is not null)
                {
                    Name = value.Name;
                    Description = value.Description ?? string.Empty;
                    SalePrice = value.SalePrice;
                    CurrentStock = value.CurrentStock;
                    MinimumStock = value.MinimumStock;
                    Barcode = value.Barcode;
                    CategoryId = value.CategoryId;
                }

                if (DeleteCommand is RelayCommand command)
                {
                    command.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public decimal CostPrice
    {
        get => _costPrice;
        set => SetProperty(ref _costPrice, value);
    }

    public decimal SalePrice
    {
        get => _salePrice;
        set => SetProperty(ref _salePrice, value);
    }

    public decimal CurrentStock
    {
        get => _currentStock;
        set => SetProperty(ref _currentStock, value);
    }

    public decimal MinimumStock
    {
        get => _minimumStock;
        set => SetProperty(ref _minimumStock, value);
    }

    public string? Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    public Guid CategoryId
    {
        get => _categoryId;
        set => SetProperty(ref _categoryId, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var results = await _searchProducts.ExecuteAsync(new SearchProductsRequest(SearchText), cancellationToken).ConfigureAwait(true);
        Products.Clear();
        foreach (var product in results)
        {
            Products.Add(product);
        }

        Message = Products.Count == 0
            ? "No encontramos productos con ese criterio. Puedes registrar uno nuevo."
            : $"Mostrando {Products.Count} producto(s). Selecciona uno para editar.";

        // Load categories if the use case is available
        if (_searchCategories is not null)
        {
            try
            {
                var categories = await _searchCategories.ExecuteAsync(cancellationToken).ConfigureAwait(true);
                Categories.Clear();
                foreach (var category in categories)
                {
                    Categories.Add(category);
                }
            }
            catch
            {
                // Category loading is best-effort; do not block product loading
            }
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (SelectedProduct is null)
            {
                var request = new CreateProductRequest(
                    Name,
                    CategoryId,
                    CostPrice,
                    SalePrice,
                    CurrentStock,
                    MinimumStock,
                    Description,
                    Barcode);
                await _createProduct.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
                Message = "Producto creado exitosamente.";
            }
            else
            {
                var request = new UpdateProductRequest(
                    SelectedProduct.Id,
                    Name,
                    CategoryId,
                    CostPrice,
                    SalePrice,
                    MinimumStock,
                    Description,
                    Barcode);
                await _updateProduct.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
                Message = "Producto actualizado exitosamente.";
            }

            ClearForm();
            await LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (ArgumentException ex)
        {
            Message = ex.Message;
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedProduct is null)
        {
            Message = "Selecciona un producto para eliminar.";
            return;
        }

        try
        {
            await _deleteProduct.ExecuteAsync(SelectedProduct.Id, cancellationToken).ConfigureAwait(true);
            Message = "Producto desactivado. Ya no aparecerá en búsquedas activas.";
            ClearForm();
            await LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }

    private void ClearForm()
    {
        SelectedProduct = null;
        Name = string.Empty;
        Description = string.Empty;
        CostPrice = 0;
        SalePrice = 0;
        CurrentStock = 0;
        MinimumStock = 0;
        Barcode = null;
        CategoryId = Guid.Empty;
    }
}
