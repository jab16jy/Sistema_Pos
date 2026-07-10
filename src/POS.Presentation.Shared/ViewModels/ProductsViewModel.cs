using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Inventory;
using POS.Application.DTOs.Products;
using POS.Application.UseCases.Categories;
using POS.Application.UseCases.Inventory;
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
    private readonly AdjustStockUseCase? _adjustStock;
    private ProductSearchResult? _selectedProduct;
    private string _searchText = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private decimal? _costPrice;
    private decimal _salePrice;
    private decimal _currentStock;
    private decimal _minimumStock;
    private decimal _taxRate = 19m;
    private int? _unitsPerBox;
    private decimal? _boxPrice;
    private string _csvPath = string.Empty;
    private string? _barcode;
    private Guid _categoryId;
    private string _message = "Busca productos por nombre o código de barras.";
    private bool _isCsvSectionOpen;
    private bool _isProductsSubtabActive = true;
    private CancellationTokenSource? _searchCancellationTokenSource;

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
        ImportProductsCsvUseCase? importProductsCsv = null,
        AdjustStockUseCase? adjustStock = null)
    {
        _createProduct = createProduct;
        _updateProduct = updateProduct;
        _deleteProduct = deleteProduct;
        _searchProducts = searchProducts;
        _searchCategories = searchCategories;
        _importProductsCsv = importProductsCsv;
        _adjustStock = adjustStock;
        Categories = [];
        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar productos: {ex.Message}"));
        SaveCommand = new RelayCommand(_ => RunSafe(SaveAsync(), ex => Message = $"Error al guardar producto: {ex.Message}"));
        DeleteCommand = new RelayCommand(_ => RunSafe(DeleteAsync(), ex => Message = $"Error al eliminar producto: {ex.Message}"), _ => SelectedProduct is not null);
        ClearCommand = new RelayCommand(_ => NewProduct());
        ImportCsvCommand = new RelayCommand(_ => RunSafe(ImportCsvAsync(), ex => Message = $"Error al importar CSV: {ex.Message}"), _ => _importProductsCsv is not null);
        NewCommand = new RelayCommand(_ => NewProduct());
        ToggleCsvCommand = new RelayCommand(_ => IsCsvSectionOpen = !IsCsvSectionOpen);
    }

    public ObservableCollection<ProductSearchResult> Products { get; } = [];

    public ObservableCollection<CategoryResult> Categories { get; }

    private CategoriesViewModel? _categoriesEditor;

    /// <summary>
    /// Optional sub-ViewModel for category CRUD operations.
    /// Set by App.xaml.cs when the category use cases are available.
    /// </summary>
    public CategoriesViewModel? CategoriesEditor
    {
        get => _categoriesEditor;
        set
        {
            if (_categoriesEditor != null)
            {
                _categoriesEditor.CategoriesChanged -= OnCategoriesChanged;
            }
            _categoriesEditor = value;
            if (_categoriesEditor != null)
            {
                _categoriesEditor.CategoriesChanged += OnCategoriesChanged;
            }
        }
    }

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
                    CostPrice = value.CostPrice;
                    SalePrice = value.SalePrice;
                    CurrentStock = value.CurrentStock;
                    MinimumStock = value.MinimumStock;
                    TaxRate = value.TaxRate;
                    Barcode = value.Barcode;
                    CategoryId = value.CategoryId;
                    UnitsPerBox = value.UnitsPerBox;
                    BoxPrice = value.BoxPrice;
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
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ReloadProductsForSearch();
            }
        }
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

    public decimal? CostPrice
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

    /// <summary>Units contained in one box; null/0 means the product is sold by unit only.</summary>
    public int? UnitsPerBox
    {
        get => _unitsPerBox;
        set
        {
            if (SetProperty(ref _unitsPerBox, value))
            {
                OnPropertyChanged(nameof(SellsByBox));
            }
        }
    }

    /// <summary>Independent price for one box (not necessarily UnitsPerBox * SalePrice).</summary>
    public decimal? BoxPrice
    {
        get => _boxPrice;
        set
        {
            if (SetProperty(ref _boxPrice, value))
            {
                OnPropertyChanged(nameof(SellsByBox));
            }
        }
    }

    public bool SellsByBox => _unitsPerBox is > 0 && _boxPrice is > 0m;

    /// <summary>Operator applying stock adjustments, for audit traceability. Set by the shell after login.</summary>
    public Guid OperatorUserId { get; set; }

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

    public decimal MarginAmount => _salePrice - (_costPrice ?? 0m);

    public string MarginPercent
    {
        get
        {
            if (!_costPrice.HasValue || _costPrice.Value == 0) return "0";
            return ((_salePrice - _costPrice.Value) / _costPrice.Value * 100m).ToString("F1", CultureInfo.InvariantCulture);
        }
    }

    public bool HasMarginPreview => _costPrice.HasValue && _costPrice.Value > 0 && _salePrice > 0;

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
        try
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

            if (_searchCategories is not null)
            {
                await LoadCategoriesAsync(cancellationToken).ConfigureAwait(true);
            }

            if (CategoriesEditor is not null && CategoriesEditor.Categories.Count == 0)
            {
                await CategoriesEditor.LoadAsync(cancellationToken).ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar productos: {ex.Message}";
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        // A box needs both values; a half-filled or "box of 1" config is treated as unit-only.
        int? unitsPerBox = null;
        decimal? boxPrice = null;
        if (_unitsPerBox is int units && units >= 2 && _boxPrice is decimal price && price > 0m)
        {
            unitsPerBox = units;
            boxPrice = price;
        }

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
                    TaxRate,
                    unitsPerBox,
                    boxPrice);
                await _createProduct.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
                Message = "Producto guardado correctamente";
            }
            else
            {
                decimal originalStock = SelectedProduct.CurrentStock;
                var request = new UpdateProductRequest(
                    SelectedProduct.Id,
                    Name,
                    CategoryId,
                    CostPrice,
                    SalePrice,
                    MinimumStock,
                    Description,
                    Barcode,
                    TaxRate,
                    unitsPerBox,
                    boxPrice);
                await _updateProduct.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
                await ApplyStockAdjustmentAsync(SelectedProduct.Id, originalStock, CurrentStock, cancellationToken).ConfigureAwait(true);
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

    /// <summary>
    /// Persists an edit to an existing product's stock as an audited inventory adjustment
    /// (UpdateProductUseCase intentionally does not touch stock). Reuses AdjustStockUseCase
    /// so the change is recorded as an InventoryMovement for traceability.
    /// </summary>
    private async Task ApplyStockAdjustmentAsync(Guid productId, decimal originalStock, decimal newStock, CancellationToken cancellationToken)
    {
        decimal delta = newStock - originalStock;
        if (delta == 0m || _adjustStock is null)
        {
            return;
        }

        var request = new AdjustStockRequest(
            productId,
            delta,
            "Ajuste manual al editar el producto",
            OperatorUserId);
        await _adjustStock.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
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
        CostPrice = null;
        SalePrice = 0;
        CurrentStock = 0;
        MinimumStock = 0;
        TaxRate = 19m;
        UnitsPerBox = null;
        BoxPrice = null;
        Barcode = null;
        CategoryId = ResolveDefaultCategoryId();
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

    private void ReloadProductsForSearch()
    {
        _searchCancellationTokenSource?.Cancel();
        _searchCancellationTokenSource?.Dispose();
        _searchCancellationTokenSource = new CancellationTokenSource();
        RunSafe(LoadAsync(_searchCancellationTokenSource.Token), ex => Message = $"Error al cargar productos: {ex.Message}");
    }

    private async Task LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var categories = await _searchCategories!.ExecuteAsync(cancellationToken).ConfigureAwait(true);
            Categories.Clear();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }

            if (SelectedProduct is null)
            {
                CategoryId = ResolveDefaultCategoryId();
            }
        }
        catch
        {
            // Category loading is best-effort; do not block product loading.
        }
    }

    private Guid ResolveDefaultCategoryId()
    {
        CategoryResult? preferredCategory = Categories.FirstOrDefault(category =>
            string.Equals(category.Name, "Sin categoría", StringComparison.OrdinalIgnoreCase));

        return preferredCategory?.Id ?? Categories.FirstOrDefault()?.Id ?? Guid.Empty;
    }

    private void OnCategoriesChanged(object? sender, EventArgs e)
    {
        RunSafe(LoadCategoriesAsync(default), ex => Message = $"Error al sincronizar categorias: {ex.Message}");
    }
}
