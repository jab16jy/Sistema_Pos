using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;
using POS.Application.UseCases.Purchases;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class PurchasesViewModel : ViewModelBase
{
    private readonly SaveSupplierUseCase _saveSupplier;
    private readonly SearchSuppliersUseCase _searchSuppliers;
    private readonly RegisterPurchaseUseCase _registerPurchase;
    private readonly Func<Guid> _currentOperatorIdProvider;
    private readonly IProductLookupService? _productLookup;
    private readonly GetSupplierCatalogUseCase? _catalog;
    private readonly GetEligibleSupplierCatalogUseCase? _eligibleCatalog;
    private readonly SaveSupplierCatalogItemUseCase? _saveCatalogItem;
    private readonly SearchPurchaseHistoryUseCase? _history;
    private readonly RelayCommand _addDraftItemCommand;
    private readonly RelayCommand _toggleProductSelectionCommand;
    private readonly RelayCommand _addAllSelectedCommand;
    private SupplierSearchResult? _selectedSupplier;
    private SupplierCatalogItemResult? _selectedCatalogItem;
    private SupplierCatalogItemResult? _selectedProfileCatalogItem;
    private ProductLookupResult? _selectedCatalogProduct;
    private Guid? _editingSupplierId;
    private string _supplierName = string.Empty;
    private string _supplierTaxId = string.Empty;
    private string _supplierPhone = string.Empty;
    private string _supplierEmail = string.Empty;
    private bool _isSupplierActive = true;
    private string _supplierSearchText = string.Empty;
    private string _catalogProductSearchText = string.Empty;
    private string _catalogSupplierSku = string.Empty;
    private string _catalogPurchaseUnit = string.Empty;
    private decimal _catalogFactor = 1m;
    private decimal _catalogCost;
    private bool _catalogIsActive = true;
    private decimal _draftQuantity = 1m;
    private string _draftProductId = string.Empty;
    private decimal _draftTotalCost;
    private string _productSearchText = string.Empty;
    private ProductLookupResult? _selectedProductResult;
    private decimal _draftCostOverride;
    private bool _useDraftCostOverride;
    private string _purchaseInvoiceNumber = string.Empty;
    private DateTime? _purchaseDate = DateTime.Today;
    private string _paymentMethod = "Contado";
    private string _paymentTerms = string.Empty;
    private string _observations = string.Empty;
    private decimal _vatRate = 19m;
    private string _activeTab = "New";
    private int _currentStep = 1;
    private DateTime? _historyFrom = DateTime.Today.AddMonths(-1);
    private DateTime? _historyTo = DateTime.Today;
    private string _historyInvoiceFilter = string.Empty;
    private int _historyPage = 1;
    private string _message = "Selecciona un proveedor para editar su perfil o registrar una compra.";

    public PurchasesViewModel(
        SaveSupplierUseCase saveSupplier,
        SearchSuppliersUseCase searchSuppliers,
        RegisterPurchaseUseCase registerPurchase,
        Func<Guid> currentOperatorIdProvider,
        IProductLookupService? productLookup = null,
        GetSupplierCatalogUseCase? catalog = null,
        GetEligibleSupplierCatalogUseCase? eligibleCatalog = null,
        SaveSupplierCatalogItemUseCase? saveCatalogItem = null,
        SearchPurchaseHistoryUseCase? history = null)
    {
        _saveSupplier = saveSupplier;
        _searchSuppliers = searchSuppliers;
        _registerPurchase = registerPurchase;
        _currentOperatorIdProvider = currentOperatorIdProvider;
        _productLookup = productLookup;
        _catalog = catalog;
        _eligibleCatalog = eligibleCatalog;
        _saveCatalogItem = saveCatalogItem;
        _history = history;
        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar proveedores: {ex.Message}"));
        NewSupplierCommand = new RelayCommand(_ => NewSupplier());
        CancelSupplierEditCommand = new RelayCommand(_ => CancelSupplierEdit());
        SaveSupplierCommand = new RelayCommand(_ => RunSafe(SaveSupplierAsync(), ex => Message = $"Error al guardar proveedor: {ex.Message}"));
        SaveCatalogItemCommand = new RelayCommand(_ => RunSafe(SaveCatalogItemAsync(), ex => Message = $"Error al guardar relación: {ex.Message}"));
        AddProductToCatalogDirectlyCommand = new RelayCommand(parameter =>
        {
            if (parameter is ProductLookupResult product)
            {
                RunSafe(AddProductToCatalogDirectlyAsync(product), ex => Message = $"Error al asociar producto: {ex.Message}");
            }
        });
        _toggleProductSelectionCommand = new RelayCommand(parameter =>
        {
            if (parameter is ProductLookupResult product)
            {
                ToggleProductSelection(product);
            }
        });
        ToggleProductSelectionCommand = _toggleProductSelectionCommand;
        _addAllSelectedCommand = new RelayCommand(_ =>
        {
            RunSafe(AddAllSelectedProductsToCatalogAsync(), ex => Message = $"Error al asociar productos: {ex.Message}");
        }, _ => SelectedCatalogProductIds.Count > 0 && EditingSupplierId is not null);
        AddAllSelectedCommand = _addAllSelectedCommand;
        DeactivateCatalogItemCommand = new RelayCommand(item => DeactivateCatalogItem(item as SupplierCatalogItemResult));
        RegisterPurchaseCommand = new RelayCommand(_ => RunSafe(RegisterPurchaseAsync(), ex => Message = $"Error al registrar compra: {ex.Message}"));
        LoadHistoryCommand = new RelayCommand(_ => RunSafe(LoadHistoryAsync(), ex => Message = $"Error al consultar historial: {ex.Message}"));
        SetTabCommand = new RelayCommand(parameter => ActiveTab = parameter as string ?? "New");
        _addDraftItemCommand = new RelayCommand(_ => AddDraftItem(), _ =>
            (SelectedCatalogItem is not null || (_catalog is null && (SelectedProductResult is not null || HasLegacyDraftProduct()) && DraftTotalCost > 0m))
            && DraftQuantity > 0m && (!UseDraftCostOverride || DraftCostOverride > 0m));
        AddDraftItemCommand = _addDraftItemCommand;
        RemoveDraftItemCommand = new RelayCommand(parameter => { if (parameter is PurchaseDraftItem item) { Items.Remove(item); RecalculateTotals(); } });
        GoToStepCommand = new RelayCommand(parameter =>
        {
            if (parameter is int stepInt)
            {
                CurrentStep = stepInt;
            }
            else if (parameter is string stepStr && int.TryParse(stepStr, out int stepParsed))
            {
                CurrentStep = stepParsed;
            }
        });
        ClearCatalogProductSelectionCommand = new RelayCommand(_ =>
        {
            SelectedCatalogProduct = null;
            CatalogProductSearchText = string.Empty;
            SelectedCatalogProductIds.Clear();
            OnPropertyChanged(nameof(SelectedProductCount));
            _addAllSelectedCommand.RaiseCanExecuteChanged();
        });
    }

    public ObservableCollection<SupplierSearchResult> Suppliers { get; } = [];
    public ObservableCollection<ProductLookupResult> CatalogProductResults { get; } = [];
    public ObservableCollection<ProductLookupResult> ProductResults => CatalogProductResults;
    public ObservableCollection<SupplierCatalogItemResult> SupplierCatalogItems { get; } = [];
    public ObservableCollection<SupplierCatalogItemResult> EligibleCatalogItems { get; } = [];
    public ObservableCollection<PurchaseDraftItem> Items { get; } = [];
    public ObservableCollection<PurchaseHistoryResult> History { get; } = [];
    public HashSet<Guid> SelectedCatalogProductIds { get; } = [];
    public ICommand LoadCommand { get; }
    public ICommand NewSupplierCommand { get; }
    public ICommand CancelSupplierEditCommand { get; }
    public ICommand SaveSupplierCommand { get; }
    public ICommand SaveCatalogItemCommand { get; }
    public ICommand AddProductToCatalogDirectlyCommand { get; }
    public ICommand ToggleProductSelectionCommand { get; }
    public ICommand AddAllSelectedCommand { get; }
    public ICommand DeactivateCatalogItemCommand { get; }
    public ICommand RegisterPurchaseCommand { get; }
    public ICommand LoadHistoryCommand { get; }
    public ICommand SetTabCommand { get; }
    public ICommand AddDraftItemCommand { get; }
    public ICommand RemoveDraftItemCommand { get; }
    public ICommand GoToStepCommand { get; }
    public ICommand ClearCatalogProductSelectionCommand { get; }

    public SupplierSearchResult? SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (!SetProperty(ref _selectedSupplier, value)) return;
            if (value is null) return;
            EditingSupplierId = value.Id;
            SupplierName = value.Name;
            SupplierTaxId = value.TaxId;
            SupplierPhone = value.Phone ?? string.Empty;
            SupplierEmail = value.Email ?? string.Empty;
            IsSupplierActive = value.IsActive;
            RunSafe(LoadCatalogsAsync(value.Id), ex => Message = $"Error al cargar catálogo: {ex.Message}");
            Message = $"Editando proveedor: {value.Name}.";
        }
    }

    public SupplierCatalogItemResult? SelectedCatalogItem { get => _selectedCatalogItem; set { if (SetProperty(ref _selectedCatalogItem, value)) _addDraftItemCommand.RaiseCanExecuteChanged(); } }
    public SupplierCatalogItemResult? SelectedProfileCatalogItem
    {
        get => _selectedProfileCatalogItem;
        set
        {
            if (!SetProperty(ref _selectedProfileCatalogItem, value) || value is null) return;
            SelectedCatalogProduct = new ProductLookupResult(value.ProductId, value.ProductName, value.ProductReference ?? string.Empty, 0m, 0m);
            CatalogSupplierSku = value.SupplierSku ?? string.Empty;
            CatalogPurchaseUnit = value.PurchaseUnitName ?? string.Empty;
            CatalogFactor = value.UnitsPerPurchaseUnit ?? 0m;
            CatalogCost = value.PurchaseUnitCost ?? 0m;
            CatalogIsActive = value.IsActive;
        }
    }
    public ProductLookupResult? SelectedCatalogProduct
    {
        get => _selectedCatalogProduct;
        set
        {
            if (SetProperty(ref _selectedCatalogProduct, value))
            {
                OnPropertyChanged(nameof(IsCatalogProductSuggestionsVisible));
                if (value is not null)
                {
                    CatalogSupplierSku = string.IsNullOrEmpty(value.Barcode) ? string.Empty : value.Barcode;
                    CatalogPurchaseUnit = "Unidad";
                    CatalogFactor = 1m;
                    CatalogCost = value.UnitPrice > 0m ? decimal.Round(value.UnitPrice * 0.70m, 2, MidpointRounding.AwayFromZero) : 10.0m;
                    CatalogIsActive = true;
                }
            }
        }
    }
    public bool IsCatalogProductSuggestionsVisible => CatalogProductResults.Count > 0;
    public Guid? EditingSupplierId
    {
        get => _editingSupplierId;
        private set { if (SetProperty(ref _editingSupplierId, value)) OnPropertyChanged(nameof(IsEditingSupplier)); }
    }
    public bool IsEditingSupplier => EditingSupplierId is not null;
    public string SupplierName { get => _supplierName; set => SetProperty(ref _supplierName, value); }
    public string SupplierTaxId { get => _supplierTaxId; set => SetProperty(ref _supplierTaxId, value); }
    public string SupplierPhone { get => _supplierPhone; set => SetProperty(ref _supplierPhone, value); }
    public string SupplierEmail { get => _supplierEmail; set => SetProperty(ref _supplierEmail, value); }
    public bool IsSupplierActive { get => _isSupplierActive; set => SetProperty(ref _isSupplierActive, value); }
    public string SupplierSearchText { get => _supplierSearchText; set { if (SetProperty(ref _supplierSearchText, value)) RunSafe(LoadAsync(), ex => Message = $"Error al cargar proveedores: {ex.Message}"); } }
    public string CatalogProductSearchText { get => _catalogProductSearchText; set { if (SetProperty(ref _catalogProductSearchText, value)) RunSafe(SearchCatalogProductsAsync(value), ex => Message = $"Error al buscar productos: {ex.Message}"); } }
    public string CatalogSupplierSku { get => _catalogSupplierSku; set => SetProperty(ref _catalogSupplierSku, value); }
    public string CatalogPurchaseUnit { get => _catalogPurchaseUnit; set => SetProperty(ref _catalogPurchaseUnit, value); }
    public decimal CatalogFactor { get => _catalogFactor; set => SetProperty(ref _catalogFactor, value); }
    public decimal CatalogCost { get => _catalogCost; set => SetProperty(ref _catalogCost, value); }
    public bool CatalogIsActive { get => _catalogIsActive; set => SetProperty(ref _catalogIsActive, value); }
    public decimal DraftQuantity { get => _draftQuantity; set { if (SetProperty(ref _draftQuantity, value)) _addDraftItemCommand.RaiseCanExecuteChanged(); } }
    public string DraftProductId { get => _draftProductId; set { if (SetProperty(ref _draftProductId, value)) _addDraftItemCommand.RaiseCanExecuteChanged(); } }
    public decimal DraftTotalCost { get => _draftTotalCost; set { if (SetProperty(ref _draftTotalCost, value)) _addDraftItemCommand.RaiseCanExecuteChanged(); } }
    public string ProductSearchText { get => _productSearchText; set { if (SetProperty(ref _productSearchText, value)) RunSafe(SearchLegacyProductsAsync(value), ex => Message = $"Error al buscar productos: {ex.Message}"); } }
    public ProductLookupResult? SelectedProductResult { get => _selectedProductResult; set { if (SetProperty(ref _selectedProductResult, value)) _addDraftItemCommand.RaiseCanExecuteChanged(); } }
    public decimal DraftCostOverride { get => _draftCostOverride; set { if (SetProperty(ref _draftCostOverride, value)) _addDraftItemCommand.RaiseCanExecuteChanged(); } }
    public bool UseDraftCostOverride { get => _useDraftCostOverride; set { if (SetProperty(ref _useDraftCostOverride, value)) _addDraftItemCommand.RaiseCanExecuteChanged(); } }
    public string PurchaseInvoiceNumber { get => _purchaseInvoiceNumber; set => SetProperty(ref _purchaseInvoiceNumber, value); }
    public DateTime? PurchaseDate { get => _purchaseDate; set => SetProperty(ref _purchaseDate, value); }
    public string PaymentMethod { get => _paymentMethod; set => SetProperty(ref _paymentMethod, value); }
    public string PaymentTerms { get => _paymentTerms; set => SetProperty(ref _paymentTerms, value); }
    public string Observations { get => _observations; set => SetProperty(ref _observations, value); }
    public decimal VatRate { get => _vatRate; set { if (SetProperty(ref _vatRate, value)) RecalculateTotals(); } }
    public decimal Subtotal => Items.Sum(item => item.Subtotal);
    public decimal Iva => decimal.Round(Subtotal * VatRate / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal Total => Subtotal + Iva;
    public string ItemCountLabel => $"{Items.Count} producto(s)";
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string ActiveTab
    {
        get => _activeTab;
        set
        {
            if (SetProperty(ref _activeTab, value))
            {
                OnPropertyChanged(nameof(IsNewPurchaseTabActive));
                OnPropertyChanged(nameof(IsHistoryTabActive));
                OnPropertyChanged(nameof(IsNewPurchaseSubtabActive));
                OnPropertyChanged(nameof(IsSuppliersSubtabActive));
                OnPropertyChanged(nameof(IsHistorySubtabActive));
            }
        }
    }
    public bool IsNewPurchaseTabActive => ActiveTab == "New";
    public bool IsHistoryTabActive => ActiveTab == "History";

    public bool IsNewPurchaseSubtabActive
    {
        get => ActiveTab == "New";
        set
        {
            if (value)
            {
                ActiveTab = "New";
                CurrentStep = 1;
            }
        }
    }

    public bool IsSuppliersSubtabActive
    {
        get => ActiveTab == "Suppliers";
        set
        {
            if (value)
            {
                ActiveTab = "Suppliers";
            }
        }
    }

    public bool IsHistorySubtabActive
    {
        get => ActiveTab == "History";
        set
        {
            if (value)
            {
                ActiveTab = "History";
            }
        }
    }

    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            if (SetProperty(ref _currentStep, value))
            {
                OnPropertyChanged(nameof(IsStep1Active));
                OnPropertyChanged(nameof(IsStep2Active));
                OnPropertyChanged(nameof(IsStep3Active));
            }
        }
    }

    public bool IsStep1Active => CurrentStep == 1;
    public bool IsStep2Active => CurrentStep == 2;
    public bool IsStep3Active => CurrentStep == 3;
    public DateTime? HistoryFrom { get => _historyFrom; set => SetProperty(ref _historyFrom, value); }
    public DateTime? HistoryTo { get => _historyTo; set => SetProperty(ref _historyTo, value); }
    public string HistoryInvoiceFilter { get => _historyInvoiceFilter; set => SetProperty(ref _historyInvoiceFilter, value); }
    public int HistoryPage { get => _historyPage; private set => SetProperty(ref _historyPage, value); }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<SupplierSearchResult> suppliers = await _searchSuppliers.ExecuteAsync(new SupplierSearchRequest(SupplierSearchText, true), cancellationToken).ConfigureAwait(true);
            Suppliers.Clear();
            foreach (SupplierSearchResult supplier in suppliers) Suppliers.Add(supplier);
            Message = Suppliers.Count == 0 ? "No hay proveedores. Crea uno para continuar." : "Selecciona un proveedor para editar su perfil o catálogo.";
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar proveedores: {ex.Message}";
        }
    }

    public async Task SaveSupplierAsync(CancellationToken cancellationToken = default)
    {
        bool wasEditing = EditingSupplierId is not null;
        SupplierResult saved = await _saveSupplier.ExecuteAsync(new SaveSupplierRequest(EditingSupplierId, SupplierName, SupplierTaxId, Normalize(SupplierPhone), Normalize(SupplierEmail), IsSupplierActive), cancellationToken).ConfigureAwait(true);
        await LoadAsync(cancellationToken).ConfigureAwait(true);
        if (wasEditing) SelectedSupplier = Suppliers.FirstOrDefault(supplier => supplier.Id == saved.Id);
        else NewSupplier();
        Message = wasEditing ? "Proveedor actualizado correctamente." : "Proveedor guardado correctamente.";
    }

    public async Task SaveCatalogItemAsync(CancellationToken cancellationToken = default)
    {
        if (EditingSupplierId is not Guid supplierId || SelectedCatalogProduct is null) { Message = "Selecciona un proveedor y un producto para configurar el catálogo."; return; }
        if (_saveCatalogItem is null) { Message = "La edición de catálogo no está disponible en esta composición."; return; }
        await _saveCatalogItem.ExecuteAsync(new SaveSupplierCatalogItemRequest(supplierId, SelectedCatalogProduct.ProductId, Normalize(CatalogSupplierSku), CatalogPurchaseUnit, CatalogFactor, CatalogCost, CatalogIsActive), cancellationToken).ConfigureAwait(true);
        await LoadCatalogsAsync(supplierId, cancellationToken).ConfigureAwait(true);
        SelectedCatalogProduct = null;
        CatalogProductSearchText = string.Empty;
        Message = "Relación de catálogo guardada correctamente.";
    }

    public async Task AddProductToCatalogDirectlyAsync(ProductLookupResult product, CancellationToken cancellationToken = default)
    {
        if (EditingSupplierId is not Guid supplierId) { Message = "Selecciona un proveedor para configurar el catálogo."; return; }
        if (_saveCatalogItem is null) { Message = "La edición de catálogo no está disponible en esta composición."; return; }
        if (product is null) return;

        string sku = string.IsNullOrEmpty(product.Barcode) ? string.Empty : product.Barcode;
        string unidad = "Unidad";
        decimal factor = 1m;
        decimal costo = product.UnitPrice > 0m ? decimal.Round(product.UnitPrice * 0.70m, 2, MidpointRounding.AwayFromZero) : 10.0m;
        bool isActive = true;

        try
        {
            await _saveCatalogItem.ExecuteAsync(new SaveSupplierCatalogItemRequest(supplierId, product.ProductId, sku, unidad, factor, costo, isActive), cancellationToken).ConfigureAwait(true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Supplier was not found"))
        {
            Message = "El proveedor seleccionado ya no existe. Vuelve a seleccionarlo de la lista.";
            return;
        }
        await LoadCatalogsAsync(supplierId, cancellationToken).ConfigureAwait(true);
        Message = $"Producto '{product.Name}' asociado al catálogo.";
    }

    public async Task RegisterPurchaseAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSupplier is null || !SelectedSupplier.IsActive || PurchaseDate is null || Items.Count == 0) { Message = "Selecciona un proveedor activo, fecha y líneas válidas del catálogo."; return; }
        Guid operatorId = _currentOperatorIdProvider();
        if (operatorId == Guid.Empty) { Message = "No se pudo identificar el operador."; return; }
        RegisterPurchaseResult result = await _registerPurchase.ExecuteAsync(new RegisterPurchaseRequest(SelectedSupplier.Id, operatorId, DateOnly.FromDateTime(PurchaseDate.Value), Normalize(PurchaseInvoiceNumber), Normalize(PaymentTerms), PaymentMethod, Normalize(Observations), VatRate, Items.Select(item => new RegisterPurchaseItemRequest(item.ProductId, item.Quantity, item.PurchaseUnitCostOverride)).ToList()), cancellationToken).ConfigureAwait(true);
        Items.Clear(); RecalculateTotals();
        Message = $"Compra registrada. Total: {result.TotalCost:N2}.";
    }

    public async Task LoadHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (_history is null) { Message = "El historial no está disponible en esta composición."; return; }
        PagedResult<PurchaseHistoryResult> result = await _history.ExecuteAsync(new PurchaseHistoryRequest(SelectedSupplier?.Id, HistoryFrom is null ? null : DateOnly.FromDateTime(HistoryFrom.Value), HistoryTo is null ? null : DateOnly.FromDateTime(HistoryTo.Value), Normalize(HistoryInvoiceFilter), HistoryPage), cancellationToken).ConfigureAwait(true);
        History.Clear(); foreach (PurchaseHistoryResult purchase in result.Items) History.Add(purchase);
        Message = $"Historial: {result.TotalCount} compra(s).";
    }

    private async Task LoadCatalogsAsync(Guid supplierId, CancellationToken cancellationToken = default)
    {
        SupplierCatalogItems.Clear(); EligibleCatalogItems.Clear(); SelectedCatalogItem = null;
        if (_catalog is not null)
            foreach (SupplierCatalogItemResult item in await _catalog.ExecuteAsync(supplierId, cancellationToken).ConfigureAwait(true)) SupplierCatalogItems.Add(item);
        if (_eligibleCatalog is not null)
            foreach (SupplierCatalogItemResult item in await _eligibleCatalog.ExecuteAsync(supplierId, cancellationToken).ConfigureAwait(true)) EligibleCatalogItems.Add(item);
        if (EligibleCatalogItems.Count == 0) Message = "Este proveedor no tiene productos activos y configurados. Configura su catálogo antes de comprar.";
    }

    private async Task SearchCatalogProductsAsync(string search, CancellationToken cancellationToken = default)
    {
        SelectedCatalogProduct = null;
        CatalogProductResults.Clear();
        SelectedCatalogProductIds.Clear();
        OnPropertyChanged(nameof(IsCatalogProductSuggestionsVisible));
        OnPropertyChanged(nameof(SelectedProductCount));
        _addAllSelectedCommand.RaiseCanExecuteChanged();
        if (_productLookup is null || string.IsNullOrWhiteSpace(search)) return;
        foreach (ProductLookupResult product in await _productLookup.SearchAsync(search, cancellationToken).ConfigureAwait(true)) CatalogProductResults.Add(product);
        OnPropertyChanged(nameof(IsCatalogProductSuggestionsVisible));
    }

    private async Task SearchLegacyProductsAsync(string search, CancellationToken cancellationToken = default)
    {
        CatalogProductResults.Clear();
        if (_productLookup is null || string.IsNullOrWhiteSpace(search)) return;
        foreach (ProductLookupResult product in await _productLookup.SearchAsync(search, cancellationToken).ConfigureAwait(true)) CatalogProductResults.Add(product);
        SelectedProductResult = CatalogProductResults.FirstOrDefault();
    }

    private void AddDraftItem()
    {
        if (SelectedCatalogItem is not { IsEligibleForPurchase: true } item)
        {
            // Compatibility for compositions that have not yet supplied catalog use cases.
            if (_catalog is null && (SelectedProductResult is not null || Guid.TryParse(DraftProductId, out _)) && DraftQuantity > 0m && DraftTotalCost > 0m)
            {
                Guid productId = SelectedProductResult?.ProductId ?? Guid.Parse(DraftProductId);
                string productName = SelectedProductResult?.Name ?? productId.ToString();
                Items.Add(new PurchaseDraftItem(productId, productName, null, "Unidad", 1m, DraftTotalCost / DraftQuantity, DraftQuantity));
                DraftProductId = string.Empty; DraftTotalCost = 0m; SelectedProductResult = null; CatalogProductResults.Clear(); RecalculateTotals();
                return;
            }
            Message = "Selecciona un producto de catálogo configurado y una cantidad válida.";
            return;
        }
        if (DraftQuantity <= 0m) { Message = "La cantidad debe ser mayor que cero."; return; }
        decimal? overrideCost = UseDraftCostOverride ? DraftCostOverride : null;
        if (overrideCost is <= 0m) { Message = "El costo modificado debe ser mayor que cero."; return; }
        Items.Add(new PurchaseDraftItem(item.ProductId, item.ProductName, item.SupplierSku ?? item.ProductReference, item.PurchaseUnitName!, item.UnitsPerPurchaseUnit!.Value, item.PurchaseUnitCost!.Value, DraftQuantity, overrideCost));
        DraftQuantity = 1m; DraftCostOverride = 0m; UseDraftCostOverride = false; SelectedCatalogItem = null; RecalculateTotals();
    }

    private void DeactivateCatalogItem(SupplierCatalogItemResult? item)
    {
        if (item is null) return;
        SelectedCatalogProduct = new ProductLookupResult(item.ProductId, item.ProductName, item.ProductReference ?? string.Empty, 0m, 0m);
        CatalogSupplierSku = item.SupplierSku ?? string.Empty;
        CatalogPurchaseUnit = item.PurchaseUnitName ?? string.Empty;
        CatalogFactor = item.UnitsPerPurchaseUnit ?? 0m;
        CatalogCost = item.PurchaseUnitCost ?? 0m;
        CatalogIsActive = false;
        RunSafe(SaveCatalogItemAsync(), ex => Message = $"Error al desactivar relación: {ex.Message}");
    }

    private void NewSupplier()
    {
        SelectedSupplier = null; EditingSupplierId = null; SupplierName = SupplierTaxId = SupplierPhone = SupplierEmail = string.Empty; IsSupplierActive = true;
        SupplierCatalogItems.Clear(); EligibleCatalogItems.Clear(); Message = "Nuevo proveedor. Completa nombre y NIT para guardar.";
        ActiveTab = "Suppliers";
        OnPropertyChanged(nameof(IsEditingSupplier));
    }

    private void CancelSupplierEdit() => NewSupplier();
    private bool HasLegacyDraftProduct() => Guid.TryParse(DraftProductId, out Guid _);
    private void RecalculateTotals() { OnPropertyChanged(nameof(Subtotal)); OnPropertyChanged(nameof(Iva)); OnPropertyChanged(nameof(Total)); OnPropertyChanged(nameof(ItemCountLabel)); }
    private static string? Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public int SelectedProductCount => SelectedCatalogProductIds.Count;

    public bool IsProductSelected(Guid productId) => SelectedCatalogProductIds.Contains(productId);

    private void ToggleProductSelection(ProductLookupResult product)
    {
        if (product is null) return;
        if (SelectedCatalogProductIds.Contains(product.ProductId))
        {
            SelectedCatalogProductIds.Remove(product.ProductId);
        }
        else
        {
            SelectedCatalogProductIds.Add(product.ProductId);
        }
        OnPropertyChanged(nameof(SelectedProductCount));
        _addAllSelectedCommand.RaiseCanExecuteChanged();
    }

    public async Task AddAllSelectedProductsToCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (EditingSupplierId is not Guid supplierId) { Message = "Selecciona un proveedor para configurar el catálogo."; return; }
        if (_saveCatalogItem is null) { Message = "La edición de catálogo no está disponible en esta composición."; return; }
        if (SelectedCatalogProductIds.Count == 0) { Message = "No hay productos seleccionados."; return; }

        var productsToAdd = CatalogProductResults.Where(p => SelectedCatalogProductIds.Contains(p.ProductId)).ToList();
        int addedCount = 0;
        foreach (var product in productsToAdd)
        {
            string sku = string.IsNullOrEmpty(product.Barcode) ? string.Empty : product.Barcode;
            string unidad = "Unidad";
            decimal factor = 1m;
            decimal costo = product.UnitPrice > 0m ? decimal.Round(product.UnitPrice * 0.70m, 2, MidpointRounding.AwayFromZero) : 10.0m;
            bool isActive = true;

            try
            {
                await _saveCatalogItem.ExecuteAsync(new SaveSupplierCatalogItemRequest(supplierId, product.ProductId, sku, unidad, factor, costo, isActive), cancellationToken).ConfigureAwait(true);
                addedCount++;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Supplier was not found"))
            {
                Message = $"Error: el proveedor seleccionado no existe en la base de datos. Selecciona el proveedor nuevamente de la lista.";
                return;
            }
        }

        await LoadCatalogsAsync(supplierId, cancellationToken).ConfigureAwait(true);
        SelectedCatalogProductIds.Clear();
        OnPropertyChanged(nameof(SelectedProductCount));
        _addAllSelectedCommand.RaiseCanExecuteChanged();
        Message = addedCount == 1 ? $"Producto asociado al catálogo." : $"{addedCount} productos asociados al catálogo.";
    }
}
