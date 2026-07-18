using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;
using POS.Application.UseCases.Purchases;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

/// <summary>
/// Supplier management, catalog configuration, and purchase history.
/// The "Nueva compra" flow lives in <see cref="NewPurchase"/> so its draft
/// state stays isolated from supplier editing and history filters.
/// </summary>
public sealed class PurchasesViewModel : ViewModelBase
{
    private readonly SaveSupplierUseCase _saveSupplier;
    private readonly SearchSuppliersUseCase _searchSuppliers;
    private readonly IProductLookupService? _productLookup;
    private readonly GetSupplierCatalogUseCase? _catalog;
    private readonly SaveSupplierCatalogItemUseCase? _saveCatalogItem;
    private readonly DeleteSupplierUseCase? _deleteSupplier;
    private readonly DeleteSupplierCatalogItemUseCase? _deleteCatalogItem;
    private readonly SearchPurchaseHistoryUseCase? _history;
    private readonly RelayCommand _deleteSupplierCommand;
    private readonly RelayCommand _addAllSelectedCommand;
    private SupplierSearchResult? _selectedSupplier;
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
    private string _activeTab = "New";
    private string _supplierPanelSection = "Profile";
    private bool _isSupplierFormActive;
    private DateTime? _historyFrom = DateTime.Today.AddMonths(-1);
    private DateTime? _historyTo = DateTime.Today;
    private string _historyInvoiceFilter = string.Empty;
    private int _historyPage = 1;
    private PurchaseHistoryResult? _selectedHistoryPurchase;
    private bool _isPurchaseDetailOpen;
    private string _message = "Selecciona un proveedor para editar su perfil o registrar una compra.";

    public PurchasesViewModel(
        SaveSupplierUseCase saveSupplier,
        SearchSuppliersUseCase searchSuppliers,
        RegisterPurchaseUseCase registerPurchase,
        Func<Guid>? currentOperatorIdProvider = null,
        IProductLookupService? productLookup = null,
        GetSupplierCatalogUseCase? catalog = null,
        GetEligibleSupplierCatalogUseCase? eligibleCatalog = null,
        SaveSupplierCatalogItemUseCase? saveCatalogItem = null,
        SearchPurchaseHistoryUseCase? history = null,
        DeleteSupplierUseCase? deleteSupplier = null,
        DeleteSupplierCatalogItemUseCase? deleteCatalogItem = null)
    {
        _saveSupplier = saveSupplier;
        _searchSuppliers = searchSuppliers;
        _productLookup = productLookup;
        _catalog = catalog;
        _saveCatalogItem = saveCatalogItem;
        _deleteSupplier = deleteSupplier;
        _deleteCatalogItem = deleteCatalogItem;
        _history = history;
        NewPurchase = new NewPurchaseViewModel(searchSuppliers, registerPurchase, eligibleCatalog);
        NewPurchase.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NewPurchaseViewModel.Message)) Message = NewPurchase.Message;
        };
        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar proveedores: {ex.Message}"));
        NewSupplierCommand = new RelayCommand(_ => NewSupplier());
        CancelSupplierEditCommand = new RelayCommand(_ => CancelSupplierEdit());
        SaveSupplierCommand = new RelayCommand(_ => RunSafe(SaveSupplierAsync(), ex => Message = $"Error al guardar proveedor: {ex.Message}"));
        SaveCatalogItemCommand = new RelayCommand(_ => RunSafe(SaveCatalogItemAsync(), ex => Message = $"Error al guardar relación: {ex.Message}"));
        _addAllSelectedCommand = new RelayCommand(_ =>
        {
            RunSafe(AddAllSelectedProductsToCatalogAsync(), ex => Message = $"Error al asociar productos: {ex.Message}");
        }, _ => SelectedProductCount > 0 && EditingSupplierId is not null);
        AddAllSelectedCommand = _addAllSelectedCommand;
        ToggleCatalogItemActiveCommand = new RelayCommand(item => ToggleCatalogItemActive(item as SupplierCatalogItemResult));
        DeleteCatalogItemCommand = new RelayCommand(item => RunSafe(DeleteCatalogItemAsync(item as SupplierCatalogItemResult), ex => Message = $"Error al eliminar producto asociado: {ex.Message}"));
        _deleteSupplierCommand = new RelayCommand(
            _ => RunSafe(DeleteSupplierAsync(), ex => Message = $"Error al eliminar proveedor: {ex.Message}"),
            _ => _deleteSupplier is not null && EditingSupplierId is not null);
        DeleteSupplierCommand = _deleteSupplierCommand;
        LoadHistoryCommand = new RelayCommand(_ => RunSafe(LoadHistoryAsync(), ex => Message = $"Error al consultar historial: {ex.Message}"));
        OpenPurchaseDetailCommand = new RelayCommand(parameter => OpenPurchaseDetail(parameter as PurchaseHistoryResult), parameter => parameter is PurchaseHistoryResult);
        ClosePurchaseDetailCommand = new RelayCommand(_ => ClosePurchaseDetail());
        ClearCatalogProductSelectionCommand = new RelayCommand(_ =>
        {
            SelectedCatalogProduct = null;
            CatalogProductSearchText = string.Empty;
            CatalogProductAssignments.Clear();
            OnPropertyChanged(nameof(SelectedProductCount));
            _addAllSelectedCommand.RaiseCanExecuteChanged();
        });
    }

    public NewPurchaseViewModel NewPurchase { get; }

    public ObservableCollection<SupplierSearchResult> Suppliers { get; } = [];
    public ObservableCollection<ProductLookupResult> CatalogProductResults { get; } = [];
    public ObservableCollection<SupplierProductAssignmentItem> CatalogProductAssignments { get; } = [];
    public ObservableCollection<SupplierCatalogItemResult> SupplierCatalogItems { get; } = [];
    public ObservableCollection<PurchaseHistoryResult> History { get; } = [];
    public PurchaseHistoryResult? SelectedHistoryPurchase { get => _selectedHistoryPurchase; private set => SetProperty(ref _selectedHistoryPurchase, value); }
    public bool IsPurchaseDetailOpen { get => _isPurchaseDetailOpen; private set => SetProperty(ref _isPurchaseDetailOpen, value); }
    public ICommand LoadCommand { get; }
    public ICommand NewSupplierCommand { get; }
    public ICommand CancelSupplierEditCommand { get; }
    public ICommand SaveSupplierCommand { get; }
    public ICommand SaveCatalogItemCommand { get; }
    public ICommand AddAllSelectedCommand { get; }
    public ICommand ToggleCatalogItemActiveCommand { get; }
    public ICommand DeleteCatalogItemCommand { get; }
    public ICommand DeleteSupplierCommand { get; }
    public ICommand LoadHistoryCommand { get; }
    public ICommand OpenPurchaseDetailCommand { get; }
    public ICommand ClosePurchaseDetailCommand { get; }
    public ICommand ClearCatalogProductSelectionCommand { get; }

    public SupplierSearchResult? SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (!SetProperty(ref _selectedSupplier, value)) return;
            if (value is null) return;
            SupplierPanelSection = "Profile";
            IsSupplierFormActive = true;
            EditingSupplierId = value.Id;
            SupplierName = value.Name;
            SupplierTaxId = value.TaxId;
            SupplierPhone = value.Phone ?? string.Empty;
            SupplierEmail = value.Email ?? string.Empty;
            IsSupplierActive = value.IsActive;
            RunSafe(LoadCatalogAsync(value.Id), ex => Message = $"Error al cargar catálogo: {ex.Message}");
            Message = $"Editando proveedor: {value.Name}.";
        }
    }

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
    public bool IsCatalogProductSuggestionsVisible => CatalogProductAssignments.Count > 0;
    public Guid? EditingSupplierId
    {
        get => _editingSupplierId;
        private set { if (SetProperty(ref _editingSupplierId, value)) { OnPropertyChanged(nameof(IsEditingSupplier)); _deleteSupplierCommand.RaiseCanExecuteChanged(); } }
    }
    public bool IsEditingSupplier => EditingSupplierId is not null;

    /// <summary>True once the supplier panel has a form to show — either an existing supplier is being edited or "+ Nuevo proveedor" started a blank one. Gates the empty-state placeholder vs. the panel content.</summary>
    public bool IsSupplierFormActive { get => _isSupplierFormActive; private set => SetProperty(ref _isSupplierFormActive, value); }
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

    /// <summary>Which section of the selected supplier's panel is showing: "Profile" or "Catalog". Keeps the panel from being one long scroll.</summary>
    public string SupplierPanelSection
    {
        get => _supplierPanelSection;
        set
        {
            if (SetProperty(ref _supplierPanelSection, value))
            {
                OnPropertyChanged(nameof(IsSupplierProfileSectionActive));
                OnPropertyChanged(nameof(IsSupplierCatalogSectionActive));
            }
        }
    }

    public bool IsSupplierProfileSectionActive
    {
        get => SupplierPanelSection == "Profile";
        set { if (value) SupplierPanelSection = "Profile"; }
    }

    public bool IsSupplierCatalogSectionActive
    {
        get => SupplierPanelSection == "Catalog";
        set { if (value) SupplierPanelSection = "Catalog"; }
    }

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
            await NewPurchase.LoadAsync(cancellationToken).ConfigureAwait(true);
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
        await LoadCatalogAsync(supplierId, cancellationToken).ConfigureAwait(true);
        SelectedCatalogProduct = null;
        CatalogProductSearchText = string.Empty;
        Message = "Relación de catálogo guardada correctamente.";
    }

    public async Task LoadHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (_history is null) { Message = "El historial no está disponible en esta composición."; return; }
        Guid? openPurchaseId = IsPurchaseDetailOpen ? SelectedHistoryPurchase?.Id : null;
        PagedResult<PurchaseHistoryResult> result = await _history.ExecuteAsync(new PurchaseHistoryRequest(SelectedSupplier?.Id, HistoryFrom is null ? null : DateOnly.FromDateTime(HistoryFrom.Value), HistoryTo is null ? null : DateOnly.FromDateTime(HistoryTo.Value), Normalize(HistoryInvoiceFilter), HistoryPage), cancellationToken).ConfigureAwait(true);
        History.Clear();
        foreach (PurchaseHistoryResult purchase in result.Items)
        {
            History.Add(purchase);
        }

        if (openPurchaseId is Guid purchaseId)
        {
            PurchaseHistoryResult? refreshedPurchase = History.FirstOrDefault(purchase => purchase.Id == purchaseId);
            if (refreshedPurchase is null)
            {
                ClosePurchaseDetail();
            }
            else
            {
                SelectedHistoryPurchase = refreshedPurchase;
                IsPurchaseDetailOpen = true;
            }
        }

        Message = $"Historial: {result.TotalCount} compra(s).";
    }

    private void OpenPurchaseDetail(PurchaseHistoryResult? purchase)
    {
        if (purchase is null)
        {
            return;
        }

        SelectedHistoryPurchase = purchase;
        IsPurchaseDetailOpen = true;
    }

    private void ClosePurchaseDetail()
    {
        IsPurchaseDetailOpen = false;
        SelectedHistoryPurchase = null;
    }

    private async Task LoadCatalogAsync(Guid supplierId, CancellationToken cancellationToken = default)
    {
        SupplierCatalogItems.Clear();
        if (_catalog is null) return;
        foreach (SupplierCatalogItemResult item in await _catalog.ExecuteAsync(supplierId, cancellationToken).ConfigureAwait(true)) SupplierCatalogItems.Add(item);
    }

    private async Task SearchCatalogProductsAsync(string search, CancellationToken cancellationToken = default)
    {
        SelectedCatalogProduct = null;
        CatalogProductResults.Clear();
        CatalogProductAssignments.Clear();
        OnPropertyChanged(nameof(IsCatalogProductSuggestionsVisible));
        OnPropertyChanged(nameof(SelectedProductCount));
        _addAllSelectedCommand.RaiseCanExecuteChanged();
        if (_productLookup is null || string.IsNullOrWhiteSpace(search)) return;
        foreach (ProductLookupResult product in await _productLookup.SearchAsync(search, cancellationToken).ConfigureAwait(true))
        {
            CatalogProductResults.Add(product);
            var assignment = new SupplierProductAssignmentItem(product);
            assignment.PropertyChanged += OnCatalogProductAssignmentChanged;
            CatalogProductAssignments.Add(assignment);
        }
        OnPropertyChanged(nameof(IsCatalogProductSuggestionsVisible));
    }

    private void OnCatalogProductAssignmentChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SupplierProductAssignmentItem.IsSelected)) return;
        OnPropertyChanged(nameof(SelectedProductCount));
        _addAllSelectedCommand.RaiseCanExecuteChanged();
    }

    private void ToggleCatalogItemActive(SupplierCatalogItemResult? item)
    {
        if (item is null) return;
        SelectedCatalogProduct = new ProductLookupResult(item.ProductId, item.ProductName, item.ProductReference ?? string.Empty, 0m, 0m);
        CatalogSupplierSku = item.SupplierSku ?? string.Empty;
        CatalogPurchaseUnit = item.PurchaseUnitName ?? string.Empty;
        CatalogFactor = item.UnitsPerPurchaseUnit ?? 0m;
        CatalogCost = item.PurchaseUnitCost ?? 0m;
        CatalogIsActive = !item.IsActive;
        RunSafe(SaveCatalogItemAsync(), ex => Message = $"Error al cambiar el estado de la relación: {ex.Message}");
    }

    public async Task DeleteCatalogItemAsync(SupplierCatalogItemResult? item, CancellationToken cancellationToken = default)
    {
        if (_deleteCatalogItem is null) { Message = "La eliminación de catálogo no está disponible en esta composición."; return; }
        if (item is null || EditingSupplierId is not Guid supplierId) { Message = "Selecciona un producto del catálogo para eliminar."; return; }

        try
        {
            await _deleteCatalogItem.ExecuteAsync(supplierId, item.ProductId, cancellationToken).ConfigureAwait(true);
            await LoadCatalogAsync(supplierId, cancellationToken).ConfigureAwait(true);
            Message = $"Producto '{item.ProductName}' eliminado del catálogo.";
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }

    public async Task DeleteSupplierAsync(CancellationToken cancellationToken = default)
    {
        if (_deleteSupplier is null) { Message = "La eliminación de proveedores no está disponible en esta composición."; return; }
        if (EditingSupplierId is not Guid supplierId) { Message = "Selecciona un proveedor para eliminar."; return; }

        try
        {
            string name = SupplierName;
            await _deleteSupplier.ExecuteAsync(supplierId, cancellationToken).ConfigureAwait(true);
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            NewSupplier();
            Message = $"Proveedor '{name}' eliminado correctamente.";
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }

    private void NewSupplier()
    {
        SelectedSupplier = null; EditingSupplierId = null; SupplierName = SupplierTaxId = SupplierPhone = SupplierEmail = string.Empty; IsSupplierActive = true;
        SupplierPanelSection = "Profile";
        IsSupplierFormActive = true;
        SupplierCatalogItems.Clear();
        Message = "Nuevo proveedor. Completa nombre y NIT para guardar.";
        ActiveTab = "Suppliers";
        OnPropertyChanged(nameof(IsEditingSupplier));
    }

    private void CancelSupplierEdit() => NewSupplier();
    private static string? Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public int SelectedProductCount => CatalogProductAssignments.Count(item => item.IsSelected);

    public async Task AddAllSelectedProductsToCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (EditingSupplierId is not Guid supplierId) { Message = "Selecciona un proveedor para configurar el catálogo."; return; }
        if (_saveCatalogItem is null) { Message = "La edición de catálogo no está disponible en esta composición."; return; }

        var productsToAdd = CatalogProductAssignments.Where(item => item.IsSelected).Select(item => item.Product).ToList();
        if (productsToAdd.Count == 0) { Message = "No hay productos seleccionados."; return; }

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

        await LoadCatalogAsync(supplierId, cancellationToken).ConfigureAwait(true);
        CatalogProductAssignments.Clear();
        OnPropertyChanged(nameof(SelectedProductCount));
        _addAllSelectedCommand.RaiseCanExecuteChanged();
        Message = addedCount == 1 ? $"Producto asociado al catálogo." : $"{addedCount} productos asociados al catálogo.";
    }
}
