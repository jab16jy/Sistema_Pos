using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using System.IO;
using ClosedXML.Excel;
using POS.Application.DTOs.BarcodeLabels;
using POS.Application.DTOs.Inventory;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Purchases;
using POS.Application.UseCases.BarcodeLabels;
using POS.Application.UseCases.Categories;
using POS.Application.UseCases.Inventory;
using POS.Application.UseCases.Products;
using POS.Application.UseCases.Purchases;
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
    private readonly AssignInternalBarcodeUseCase? _assignInternalBarcode;
    private readonly PrintLabelsUseCase? _printLabels;
    private readonly GetProductSupplierSummaryUseCase? _supplierSummary;
    private readonly RelayCommand _generateInternalBarcodeCommand;
    private readonly RelayCommand _printLabelCommand;
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
        : this(createProduct, updateProduct, deleteProduct, searchProducts, null, null, null, null, null, null)
    {
    }

    public ProductsViewModel(
        CreateProductUseCase createProduct,
        UpdateProductUseCase updateProduct,
        DeleteProductUseCase deleteProduct,
        SearchProductsUseCase searchProducts,
        SearchCategoriesUseCase? searchCategories,
        ImportProductsCsvUseCase? importProductsCsv = null,
        AdjustStockUseCase? adjustStock = null,
        AssignInternalBarcodeUseCase? assignInternalBarcode = null,
        PrintLabelsUseCase? printLabels = null,
        GetProductSupplierSummaryUseCase? supplierSummary = null)
    {
        _createProduct = createProduct;
        _updateProduct = updateProduct;
        _deleteProduct = deleteProduct;
        _searchProducts = searchProducts;
        _searchCategories = searchCategories;
        _importProductsCsv = importProductsCsv;
        // ponytail: adjustStock kept in the signature for DI/test compat; stock edits now live in
        // Inventario, so Productos no longer applies adjustments from the product form.
        _ = adjustStock;
        _assignInternalBarcode = assignInternalBarcode;
        _printLabels = printLabels;
        _supplierSummary = supplierSummary;
        Categories = [];
        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar productos: {ex.Message}"));
        SaveCommand = new RelayCommand(_ => RunSafe(SaveAsync(), ex => Message = $"Error al guardar producto: {ex.Message}"));
        DeleteCommand = new RelayCommand(_ => RunSafe(DeleteAsync(), ex => Message = $"Error al eliminar producto: {ex.Message}"), _ => SelectedProduct is not null);
        ClearCommand = new RelayCommand(_ => NewProduct());
        ImportCsvCommand = new RelayCommand(_ => RunSafe(ImportCsvAsync(), ex => Message = $"Error al importar CSV: {ex.Message}"), _ => _importProductsCsv is not null);
        NewCommand = new RelayCommand(_ => NewProduct());
        ToggleCsvCommand = new RelayCommand(_ => IsCsvSectionOpen = !IsCsvSectionOpen);
        _generateInternalBarcodeCommand = new RelayCommand(_ => RunSafe(GenerateInternalBarcodeAsync(), ex => Message = $"Error al asignar código interno: {ex.Message}"), _ => SelectedProduct is not null && string.IsNullOrWhiteSpace(SelectedProduct.Barcode));
        _printLabelCommand = new RelayCommand(_ => RunSafe(PrintLabelAsync(), ex => Message = $"Error al imprimir la etiqueta: {ex.Message}"), _ => SelectedProduct is not null && !string.IsNullOrWhiteSpace(SelectedProduct.Barcode));
        GenerateInternalBarcodeCommand = _generateInternalBarcodeCommand;
        PrintLabelCommand = _printLabelCommand;
    }

    public ObservableCollection<ProductSearchResult> Products { get; } = [];

    public ObservableCollection<CategoryResult> Categories { get; }

    public ObservableCollection<SupplierCatalogItemResult> SupplierRelationships { get; } = [];

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
                _categoriesEditor.ProductsAssigned -= OnProductsAssigned;
            }
            _categoriesEditor = value;
            if (_categoriesEditor != null)
            {
                _categoriesEditor.CategoriesChanged += OnCategoriesChanged;
                _categoriesEditor.ProductsAssigned += OnProductsAssigned;
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

    public ICommand GenerateInternalBarcodeCommand { get; }

    public ICommand PrintLabelCommand { get; }

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
                    RunSafe(LoadSupplierRelationshipsAsync(value.Id), ex => Message = $"Error al cargar proveedores: {ex.Message}");
                }
                else
                {
                    SupplierRelationships.Clear();
                }

                OnPropertyChanged(nameof(HasSelectedProduct));
                OnPropertyChanged(nameof(FormTitle));
                OnPropertyChanged(nameof(ShowFormStatusChip));
                OnPropertyChanged(nameof(FormStatusChipLabel));
                OnPropertyChanged(nameof(FormStatusChipKind));
                OnPropertyChanged(nameof(IsStockEditable));
                OnPropertyChanged(nameof(StockFieldLabel));

                if (DeleteCommand is RelayCommand command)
                {
                    command.RaiseCanExecuteChanged();
                }

                _generateInternalBarcodeCommand.RaiseCanExecuteChanged();
                _printLabelCommand.RaiseCanExecuteChanged();

                RecalculateMarginPreview();
                RecalculateStockStatusPreview();
            }
        }
    }

    /// <summary>
    /// Stock is never editable from Productos, new or existing: it always starts at zero and every
    /// change goes through the Inventario module (adjustments) or the Compras/Ventas flows so saldo
    /// e historial stay traceable. Only MinimumStock is master data managed here.
    /// </summary>
    public bool IsStockEditable => false;

    public string StockFieldLabel => "Stock actual";

    private async Task LoadSupplierRelationshipsAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        SupplierRelationships.Clear();
        if (_supplierSummary is null) return;
        foreach (SupplierCatalogItemResult item in await _supplierSummary.ExecuteAsync(productId, cancellationToken).ConfigureAwait(true))
        {
            SupplierRelationships.Add(item);
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
        set
        {
            if (SetProperty(ref _isProductsSubtabActive, value))
            {
                OnPropertyChanged(nameof(IsCategoriesSubtabActive));
            }
        }
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
            Guid savedProductId;
            if (SelectedProduct is null)
            {
                // New products always start at zero stock; balances are seeded from Inventario.
                var request = new CreateProductRequest(
                    Name,
                    CategoryId,
                    CostPrice,
                    SalePrice,
                    0m,
                    MinimumStock,
                    Description,
                    Barcode,
                    TaxRate,
                    unitsPerBox,
                    boxPrice,
                    OperatorUserId);
                savedProductId = await _createProduct.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
            }
            else
            {
                // Editing a product never touches stock: CurrentStock is read-only here and any
                // change belongs to Inventario (adjustments) or Compras/Ventas.
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
                ProductResult result = await _updateProduct.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
                savedProductId = result.Id;
            }

            await LoadAsync(cancellationToken).ConfigureAwait(true);
            SelectedProduct = Products.FirstOrDefault(product => product.Id == savedProductId);
            Message = "Producto guardado correctamente";
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

    public async Task GenerateInternalBarcodeAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedProduct is null)
        {
            Message = "Guarda el producto antes de asignarle un código interno.";
            return;
        }

        if (_assignInternalBarcode is null)
        {
            Message = "La asignación de códigos internos no está disponible en este entorno.";
            return;
        }

        Guid productId = SelectedProduct.Id;
        string barcode = await _assignInternalBarcode.ExecuteAsync(productId, cancellationToken).ConfigureAwait(true);
        await LoadAsync(cancellationToken).ConfigureAwait(true);
        SelectedProduct = Products.FirstOrDefault(product => product.Id == productId);
        Barcode = barcode;
        Message = "Código interno asignado y guardado. Ya puedes imprimir la etiqueta.";
    }

    public async Task PrintLabelAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedProduct is null || string.IsNullOrWhiteSpace(SelectedProduct.Barcode))
        {
            Message = "Guarda un código de barras antes de imprimir la etiqueta.";
            return;
        }

        if (_printLabels is null)
        {
            Message = "La impresión de etiquetas no está disponible en este entorno.";
            return;
        }

        string barcode = SelectedProduct.Barcode;
        var label = new LabelRequest(
            SelectedProduct.Name,
            SelectedProduct.SalePrice,
            barcode,
            GenerateBarcodeUseCase.ResolveSymbology(barcode));
        await _printLabels.ExecuteAsync([label], cancellationToken: cancellationToken).ConfigureAwait(true);
        Message = "Etiqueta enviada a la impresora.";
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
            Message = "Escribe la ruta de un archivo CSV o XLSX existente.";
            return;
        }

        try
        {
            bool isXlsx = string.Equals(Path.GetExtension(CsvPath), ".xlsx", StringComparison.OrdinalIgnoreCase);
            var content = isXlsx
                ? ReadXlsxAsCsv(CsvPath)
                : await File.ReadAllTextAsync(CsvPath, cancellationToken).ConfigureAwait(true);
            var result = await _importProductsCsv.ExecuteAsync(new ImportProductsCsvRequest(content, OperatorUserId), cancellationToken).ConfigureAwait(true);
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

    /// <summary>Reads the first sheet of an .xlsx file and renders it as CSV text so the same import parser handles both formats.</summary>
    private static string ReadXlsxAsCsv(string path)
    {
        using var workbook = new XLWorkbook(path);
        var worksheet = workbook.Worksheets.First();
        int lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        var sb = new StringBuilder();

        foreach (var row in worksheet.RowsUsed())
        {
            var cells = new string[lastColumn];
            for (var col = 1; col <= lastColumn; col++)
            {
                // GetString() (not GetFormattedString()) so a thousands-separated price like
                // "65,000.00" never smuggles an extra comma into the CSV column split.
                cells[col - 1] = EscapeCsvField(row.Cell(col).GetString());
            }

            sb.AppendLine(string.Join(',', cells));
        }

        return sb.ToString();
    }

    private static string EscapeCsvField(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

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

        _generateInternalBarcodeCommand.RaiseCanExecuteChanged();
        _printLabelCommand.RaiseCanExecuteChanged();
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

    private void OnProductsAssigned(object? sender, EventArgs e)
    {
        RunSafe(LoadAsync(), ex => Message = $"Error al actualizar productos: {ex.Message}");
    }
}
