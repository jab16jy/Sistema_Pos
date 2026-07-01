using System.Collections.ObjectModel;
using System.Globalization;
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
    private readonly ImportProductsCsvUseCase? _importProductsCsv;
    private readonly SearchCategoriesUseCase? _searchCategories;
    private ProductSearchResult? _selectedProduct;
    private string _searchText = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private decimal _costPrice;
    private decimal _salePrice;
    private decimal _currentStock;
    private decimal _minimumStock;
    private decimal _taxRate = 19m;
    private string _csvPath = string.Empty;
    private string? _barcode;
    private Guid _categoryId;
    private string _message = "Busca productos por nombre o código de barras.";
    private bool _isCsvSectionOpen;
    private bool _isProductsSubtabActive = true;

    public ProductsViewModel(
        CreateProductUseCase createProduct,
        UpdateProductUseCase updateProduct,
        DeleteProductUseCase deleteProduct,
        SearchProductsUseCase searchProducts)
        : this(createProduct, updateProduct, deleteProduct, searchProducts, null, null)
    {
    }

    public ProductsViewModel(
        CreateProductUseCase createProduct,
        UpdateProductUseCase updateProduct,
        DeleteProductUseCase deleteProduct,
        SearchProductsUseCase searchProducts,
        SearchCategoriesUseCase? searchCategories,
        ImportProductsCsvUseCase? importProductsCsv = null)
    {
        _createProduct = createProduct;
        _updateProduct = updateProduct;
        _deleteProduct = deleteProduct;
        _searchProducts = searchProducts;
        _searchCategories = searchCategories;
        _importProductsCsv = importProductsCsv;
        Categories = [];
        LoadCommand = new RelayCommand(_ => _ = LoadAsync());
        SaveCommand = new RelayCommand(_ => _ = SaveAsync());
        DeleteCommand = new RelayCommand(_ => _ = DeleteAsync(), _ => SelectedProduct is not null);
        ClearCommand = new RelayCommand(_ => NewProduct());
        ImportCsvCommand = new RelayCommand(_ => _ = ImportCsvAsync(), _ => _importProductsCsv is not null);
        NewCommand = new RelayCommand(_ => NewProduct());
        ToggleCsvCommand = new RelayCommand(_ => IsCsvSectionOpen = !IsCsvSectionOpen);
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

    public ICommand ImportCsvCommand { get; }

    public ICommand NewCommand { get; }

    public ICommand ToggleCsvCommand { get; }

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
                    CostPrice = 0; // ProductSearchResult does not carry CostPrice
                    SalePrice = value.SalePrice;
                    CurrentStock = value.CurrentStock;
                    MinimumStock = value.MinimumStock;
                    TaxRate = value.TaxRate;
                    Barcode = value.Barcode;
                    CategoryId = value.CategoryId;
                }

                OnPropertyChanged(nameof(HasSelectedProduct));
                OnPropertyChanged(nameof(FormTitle));
                OnPropertyChanged(nameof(ShowFormStatusChip));
                OnPropertyChanged(nameof(FormStatusChipLabel));
                OnPropertyChanged(nameof(FormStatusChipKind));

                if (DeleteCommand is RelayCommand command)
                {
                    command.RaiseCanExecuteChanged();
                }

                RecalculateMarginPreview();
                RecalculateStockStatusPreview();
            }
        }
    }

    public bool HasSelectedProduct => _selectedProduct is not null;

    public string FormTitle => _selectedProduct is not null ? "Editando producto" : "Nuevo producto";

    public bool ShowFormStatusChip => _selectedProduct is not null;

    public string FormStatusChipLabel
    {
        get
        {
            if (_selectedProduct is null) return string.Empty;
            if (_selectedProduct.CurrentStock == 0) return "Sin stock";
            if (_selectedProduct.CurrentStock < _selectedProduct.MinimumStock) return "Stock bajo";
            return "En stock";
        }
    }

    public string FormStatusChipKind
    {
        get
        {
            if (_selectedProduct is null) return "neutral";
            if (_selectedProduct.CurrentStock == 0) return "error";
            if (_selectedProduct.CurrentStock < _selectedProduct.MinimumStock) return "warning";
            return "success";
        }
    }

    public bool IsProductsSubtabActive
    {
        get => _isProductsSubtabActive;
        set => SetProperty(ref _isProductsSubtabActive, value);
    }

    public bool IsCategoriesSubtabActive
    {
        get => !_isProductsSubtabActive;
        set
        {
            if (value)
            {
                IsProductsSubtabActive = false;
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
        set
        {
            if (SetProperty(ref _costPrice, value))
            {
                RecalculateMarginPreview();
            }
        }
    }

    public decimal SalePrice
    {
        get => _salePrice;
        set
        {
            if (SetProperty(ref _salePrice, value))
            {
                RecalculateMarginPreview();
            }
        }
    }

    public decimal CurrentStock
    {
        get => _currentStock;
        set
        {
            if (SetProperty(ref _currentStock, value))
            {
                RecalculateStockStatusPreview();
            }
        }
    }

    public decimal MinimumStock
    {
        get => _minimumStock;
        set
        {
            if (SetProperty(ref _minimumStock, value))
            {
                RecalculateStockStatusPreview();
            }
        }
    }

    public decimal TaxRate
    {
        get => _taxRate;
        set => SetProperty(ref _taxRate, value);
    }

    public string CsvPath
    {
        get => _csvPath;
        set => SetProperty(ref _csvPath, value);
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

    public bool IsCsvSectionOpen
    {
        get => _isCsvSectionOpen;
        set => SetProperty(ref _isCsvSectionOpen, value);
    }

    // ── Margin preview (presentation-only) ──

    public decimal MarginAmount => _salePrice - _costPrice;

    public string MarginPercent
    {
        get
        {
            if (_costPrice == 0) return "0";
            return ((_salePrice - _costPrice) / _costPrice * 100m).ToString("F1", CultureInfo.InvariantCulture);
        }
    }

    public bool HasMarginPreview => _costPrice > 0 && _salePrice > 0;

    public bool IsMarginNegative => MarginAmount < 0;

    public string MarginDisplay => $"Margen: $ {MarginAmount.ToString("N0", CultureInfo.GetCultureInfo("es-CO"))} ({MarginPercent}%)";

    // ── Stock status preview (presentation-only) ──

    public bool HasStockStatusPreview => !string.IsNullOrWhiteSpace(_name);

    public string StockStatusLabel
    {
        get
        {
            if (_currentStock == 0) return "Sin stock";
            if (_currentStock < _minimumStock) return "Stock bajo";
            return "En stock";
        }
    }

    public string StockStatusKind
    {
        get
        {
            if (_currentStock == 0) return "error";
            if (_currentStock < _minimumStock) return "warning";
            return "success";
        }
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
                    Barcode,
                    TaxRate);
                await _createProduct.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
                Message = "Producto guardado correctamente";
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
                    Barcode,
                    TaxRate);
                await _updateProduct.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
                Message = "Producto guardado correctamente";
            }

            NewProduct();
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
            Message = "Producto eliminado";
            NewProduct();
            await LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }

    public async Task ImportCsvAsync(CancellationToken cancellationToken = default)
    {
        if (_importProductsCsv is null)
        {
            Message = "La importación CSV no está disponible en este entorno.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CsvPath) || !File.Exists(CsvPath))
        {
            Message = "Escribe la ruta de un archivo CSV existente.";
            return;
        }

        try
        {
            var content = await File.ReadAllTextAsync(CsvPath, cancellationToken).ConfigureAwait(true);
            var result = await _importProductsCsv.ExecuteAsync(new ImportProductsCsvRequest(content), cancellationToken).ConfigureAwait(true);
            Message = result.Errors.Count == 0
                ? $"Importación lista: {result.Imported} producto(s), {result.Skipped} omitido(s)."
                : $"Importados {result.Imported}, omitidos {result.Skipped}. Primer error: {result.Errors[0]}";
            await LoadAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Message = ex.Message;
        }
    }

    private void NewProduct()
    {
        _selectedProduct = null;
        OnPropertyChanged(nameof(SelectedProduct));
        Name = string.Empty;
        Description = string.Empty;
        CostPrice = 0;
        SalePrice = 0;
        CurrentStock = 0;
        MinimumStock = 0;
        TaxRate = 19m;
        Barcode = null;
        CategoryId = Guid.Empty;
        OnPropertyChanged(nameof(HasSelectedProduct));
        OnPropertyChanged(nameof(FormTitle));
        OnPropertyChanged(nameof(ShowFormStatusChip));
        OnPropertyChanged(nameof(FormStatusChipLabel));
        OnPropertyChanged(nameof(FormStatusChipKind));
        RecalculateMarginPreview();
        RecalculateStockStatusPreview();

        if (DeleteCommand is RelayCommand command)
        {
            command.RaiseCanExecuteChanged();
        }
    }

    private void RecalculateMarginPreview()
    {
        OnPropertyChanged(nameof(MarginAmount));
        OnPropertyChanged(nameof(MarginPercent));
        OnPropertyChanged(nameof(HasMarginPreview));
        OnPropertyChanged(nameof(IsMarginNegative));
        OnPropertyChanged(nameof(MarginDisplay));
    }

    private void RecalculateStockStatusPreview()
    {
        OnPropertyChanged(nameof(HasStockStatusPreview));
        OnPropertyChanged(nameof(StockStatusLabel));
        OnPropertyChanged(nameof(StockStatusKind));
    }
}
