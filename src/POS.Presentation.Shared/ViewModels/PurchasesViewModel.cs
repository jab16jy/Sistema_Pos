using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Purchases;
using POS.Application.UseCases.Purchases;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class PurchasesViewModel : ViewModelBase
{
    private readonly SaveSupplierUseCase _saveSupplier;
    private readonly SearchSuppliersUseCase _searchSuppliers;
    private readonly RegisterPurchaseUseCase _registerPurchase;
    private readonly Func<Guid> _currentOperatorIdProvider;
    private readonly GetSupplierCatalogUseCase? _catalog;
    private readonly SearchPurchaseHistoryUseCase? _history;
    private readonly CreateSupplierQuotationUseCase? _createQuotation;
    private readonly AcceptSupplierQuotationUseCase? _acceptQuotation;
    private readonly ConvertSupplierQuotationToPurchaseDraftUseCase? _convertQuotation;
    private readonly RelayCommand _addDraftItemCommand;
    private SupplierSearchResult? _selectedSupplier;
    private SupplierCatalogItemResult? _selectedCatalogItem;
    private string _supplierName = string.Empty;
    private string _supplierTaxId = string.Empty;
    private string _supplierPhone = string.Empty;
    private string _supplierEmail = string.Empty;
    private string _supplierSearchText = string.Empty;
    private decimal _draftQuantity = 1m;
    private string _message = "Selecciona un proveedor activo y su catálogo de compra.";
    private bool _isNewSupplierFormOpen;
    private string _purchaseInvoiceNumber = string.Empty;
    private DateTime? _purchaseDate = DateTime.Today;
    private string _paymentMethod = "Contado";
    private string _paymentTerms = string.Empty;
    private string _observations = string.Empty;
    private decimal _vatRate = 19m;
    private string _activeTab = "New";
    private DateTime? _historyFrom = DateTime.Today.AddMonths(-1);
    private DateTime? _historyTo = DateTime.Today;
    private string _historyInvoiceFilter = string.Empty;
    private int _historyPage = 1;
    private Guid? _supplierQuotationId;
    private Guid? _lastSupplierQuotationId;

    public PurchasesViewModel(
        SaveSupplierUseCase saveSupplier,
        SearchSuppliersUseCase searchSuppliers,
        RegisterPurchaseUseCase registerPurchase,
        Func<Guid> currentOperatorIdProvider,
        GetSupplierCatalogUseCase? catalog = null,
        SearchPurchaseHistoryUseCase? history = null,
        CreateSupplierQuotationUseCase? createQuotation = null,
        AcceptSupplierQuotationUseCase? acceptQuotation = null,
        ConvertSupplierQuotationToPurchaseDraftUseCase? convertQuotation = null)
    {
        _saveSupplier = saveSupplier;
        _searchSuppliers = searchSuppliers;
        _registerPurchase = registerPurchase;
        _currentOperatorIdProvider = currentOperatorIdProvider;
        _catalog = catalog;
        _history = history;
        _createQuotation = createQuotation;
        _acceptQuotation = acceptQuotation;
        _convertQuotation = convertQuotation;
        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar proveedores: {ex.Message}"));
        SaveSupplierCommand = new RelayCommand(_ => RunSafe(SaveSupplierAsync(), ex => Message = $"Error al guardar proveedor: {ex.Message}"));
        RegisterPurchaseCommand = new RelayCommand(_ => RunSafe(RegisterPurchaseAsync(), ex => Message = $"Error al registrar compra: {ex.Message}"));
        LoadHistoryCommand = new RelayCommand(_ => RunSafe(LoadHistoryAsync(), ex => Message = $"Error al consultar historial: {ex.Message}"));
        CreateSupplierQuotationCommand = new RelayCommand(_ => RunSafe(CreateSupplierQuotationAsync(), ex => Message = $"Error al crear cotización: {ex.Message}"));
        AcceptSupplierQuotationCommand = new RelayCommand(_ => RunSafe(AcceptSupplierQuotationAsync(), ex => Message = $"Error al aceptar cotización: {ex.Message}"));
        ConvertSupplierQuotationCommand = new RelayCommand(_ => RunSafe(ConvertSupplierQuotationAsync(), ex => Message = $"Error al convertir cotización: {ex.Message}"));
        SetTabCommand = new RelayCommand(parameter => ActiveTab = parameter as string ?? "New");
        _addDraftItemCommand = new RelayCommand(_ => AddDraftItem(), _ => SelectedCatalogItem is not null && DraftQuantity > 0m);
        AddDraftItemCommand = _addDraftItemCommand;
        RemoveDraftItemCommand = new RelayCommand(parameter => { if (parameter is PurchaseDraftItem item) { Items.Remove(item); RecalculateTotals(); } });
        ToggleNewSupplierCommand = new RelayCommand(_ => IsNewSupplierFormOpen = !IsNewSupplierFormOpen);
    }

    public ObservableCollection<SupplierSearchResult> Suppliers { get; } = [];
    public ObservableCollection<SupplierCatalogItemResult> CatalogItems { get; } = [];
    public ObservableCollection<PurchaseDraftItem> Items { get; } = [];
    public ObservableCollection<PurchaseHistoryResult> History { get; } = [];
    public ICommand LoadCommand { get; }
    public ICommand SaveSupplierCommand { get; }
    public ICommand RegisterPurchaseCommand { get; }
    public ICommand LoadHistoryCommand { get; }
    public ICommand CreateSupplierQuotationCommand { get; }
    public ICommand AcceptSupplierQuotationCommand { get; }
    public ICommand ConvertSupplierQuotationCommand { get; }
    public ICommand SetTabCommand { get; }
    public ICommand AddDraftItemCommand { get; }
    public ICommand RemoveDraftItemCommand { get; }
    public ICommand ToggleNewSupplierCommand { get; }

    public SupplierSearchResult? SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (SetProperty(ref _selectedSupplier, value))
            {
                OnPropertyChanged(nameof(HasSelectedSupplier));
                OnPropertyChanged(nameof(SelectedSupplierChipLabel));
                CatalogItems.Clear();
                SelectedCatalogItem = null;
                if (value is not null) RunSafe(LoadCatalogAsync(value.Id), ex => Message = $"Error al cargar catálogo: {ex.Message}");
            }
        }
    }

    public SupplierCatalogItemResult? SelectedCatalogItem
    {
        get => _selectedCatalogItem;
        set { if (SetProperty(ref _selectedCatalogItem, value)) _addDraftItemCommand.RaiseCanExecuteChanged(); }
    }

    public bool HasSelectedSupplier => SelectedSupplier is not null;
    public string SelectedSupplierChipLabel => SelectedSupplier?.Name ?? "Selecciona un proveedor";
    public string SupplierName { get => _supplierName; set => SetProperty(ref _supplierName, value); }
    public string SupplierTaxId { get => _supplierTaxId; set => SetProperty(ref _supplierTaxId, value); }
    public string SupplierPhone { get => _supplierPhone; set => SetProperty(ref _supplierPhone, value); }
    public string SupplierEmail { get => _supplierEmail; set => SetProperty(ref _supplierEmail, value); }
    public string SupplierSearchText { get => _supplierSearchText; set { if (SetProperty(ref _supplierSearchText, value)) RunSafe(LoadAsync(), ex => Message = $"Error al cargar proveedores: {ex.Message}"); } }
    public bool IsNewSupplierFormOpen { get => _isNewSupplierFormOpen; set => SetProperty(ref _isNewSupplierFormOpen, value); }
    public string PurchaseInvoiceNumber { get => _purchaseInvoiceNumber; set => SetProperty(ref _purchaseInvoiceNumber, value); }
    public DateTime? PurchaseDate { get => _purchaseDate; set => SetProperty(ref _purchaseDate, value); }
    public string PaymentMethod { get => _paymentMethod; set => SetProperty(ref _paymentMethod, value); }
    public string PaymentTerms { get => _paymentTerms; set => SetProperty(ref _paymentTerms, value); }
    public string Observations { get => _observations; set => SetProperty(ref _observations, value); }
    public decimal VatRate { get => _vatRate; set { if (SetProperty(ref _vatRate, value)) RecalculateTotals(); } }
    public decimal DraftQuantity { get => _draftQuantity; set { if (SetProperty(ref _draftQuantity, value)) _addDraftItemCommand.RaiseCanExecuteChanged(); } }
    public decimal Subtotal => Items.Sum(item => item.Subtotal);
    public decimal Iva => decimal.Round(Subtotal * VatRate / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal Total => Subtotal + Iva;
    public string ItemCountLabel => $"{Items.Count} producto(s)";
    public string Message { get => _message; private set => SetProperty(ref _message, value); }
    public string ActiveTab { get => _activeTab; set { if (SetProperty(ref _activeTab, value)) { OnPropertyChanged(nameof(IsNewPurchaseTabActive)); OnPropertyChanged(nameof(IsHistoryTabActive)); OnPropertyChanged(nameof(IsQuotationTabActive)); } } }
    public bool IsNewPurchaseTabActive => ActiveTab == "New";
    public bool IsHistoryTabActive => ActiveTab == "History";
    public bool IsQuotationTabActive => ActiveTab == "Quotation";
    public DateTime? HistoryFrom { get => _historyFrom; set => SetProperty(ref _historyFrom, value); }
    public DateTime? HistoryTo { get => _historyTo; set => SetProperty(ref _historyTo, value); }
    public string HistoryInvoiceFilter { get => _historyInvoiceFilter; set => SetProperty(ref _historyInvoiceFilter, value); }
    public int HistoryPage { get => _historyPage; private set => SetProperty(ref _historyPage, value); }
    public Guid? LastSupplierQuotationId { get => _lastSupplierQuotationId; private set => SetProperty(ref _lastSupplierQuotationId, value); }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SupplierSearchResult> suppliers = await _searchSuppliers.ExecuteAsync(new SupplierSearchRequest(SupplierSearchText), cancellationToken).ConfigureAwait(true);
        Suppliers.Clear(); foreach (SupplierSearchResult supplier in suppliers) Suppliers.Add(supplier);
        Message = Suppliers.Count == 0 ? "No hay proveedores activos. Registra uno para continuar." : "Selecciona un proveedor activo.";
    }

    public async Task SaveSupplierAsync(CancellationToken cancellationToken = default)
    {
        await _saveSupplier.ExecuteAsync(new SaveSupplierRequest(null, SupplierName, SupplierTaxId, Normalize(SupplierPhone), Normalize(SupplierEmail), true), cancellationToken).ConfigureAwait(true);
        SupplierName = SupplierTaxId = SupplierPhone = SupplierEmail = string.Empty;
        IsNewSupplierFormOpen = false;
        await LoadAsync(cancellationToken).ConfigureAwait(true);
        Message = "Proveedor guardado.";
    }

    public async Task RegisterPurchaseAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSupplier is null || Items.Count == 0 || PurchaseDate is null) { Message = "Selecciona proveedor, fecha y al menos un producto del catálogo."; return; }
        Guid operatorId = _currentOperatorIdProvider();
        if (operatorId == Guid.Empty) { Message = "No se pudo identificar el operador."; return; }
        RegisterPurchaseResult result = await _registerPurchase.ExecuteAsync(new RegisterPurchaseRequest(SelectedSupplier.Id, operatorId, DateOnly.FromDateTime(PurchaseDate.Value), Normalize(PurchaseInvoiceNumber), Normalize(PaymentTerms), PaymentMethod, Normalize(Observations), VatRate, Items.Select(item => new RegisterPurchaseItemRequest(item.ProductId, item.Quantity)).ToList(), _supplierQuotationId), cancellationToken).ConfigureAwait(true);
        Items.Clear(); RecalculateTotals(); _supplierQuotationId = null;
        Message = $"Compra registrada. Total persistido: ${result.TotalCost:N0}.";
    }

    public async Task LoadHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (_history is null) { Message = "El historial no está disponible en esta composición."; return; }
        PagedResult<PurchaseHistoryResult> result = await _history.ExecuteAsync(new PurchaseHistoryRequest(SelectedSupplier?.Id, HistoryFrom is null ? null : DateOnly.FromDateTime(HistoryFrom.Value), HistoryTo is null ? null : DateOnly.FromDateTime(HistoryTo.Value), Normalize(HistoryInvoiceFilter), HistoryPage), cancellationToken).ConfigureAwait(true);
        History.Clear(); foreach (PurchaseHistoryResult purchase in result.Items) History.Add(purchase);
        Message = $"Historial: {result.TotalCount} compra(s).";
    }

    public async Task CreateSupplierQuotationAsync(CancellationToken cancellationToken = default)
    {
        if (_createQuotation is null || SelectedSupplier is null || PurchaseDate is null || Items.Count == 0) { Message = "Selecciona proveedor, fecha y productos para cotizar."; return; }
        SupplierQuotationResult quotation = await _createQuotation.ExecuteAsync(new CreateSupplierQuotationRequest(SelectedSupplier.Id, DateOnly.FromDateTime(PurchaseDate.Value), Normalize(PaymentTerms), Normalize(Observations), Items.Select(item => new CreateSupplierQuotationItemRequest(item.ProductId, item.Quantity)).ToList()), cancellationToken).ConfigureAwait(true);
        LastSupplierQuotationId = quotation.Id;
        Message = "Cotización de proveedor creada como borrador.";
    }

    public async Task AcceptSupplierQuotationAsync(CancellationToken cancellationToken = default)
    {
        if (_acceptQuotation is null || LastSupplierQuotationId is not Guid quotationId) { Message = "Primero crea una cotización de proveedor."; return; }
        await _acceptQuotation.ExecuteAsync(quotationId, cancellationToken).ConfigureAwait(true);
        Message = "Cotización aceptada. No se modificó inventario.";
    }

    public async Task ConvertSupplierQuotationAsync(CancellationToken cancellationToken = default)
    {
        if (_convertQuotation is null || LastSupplierQuotationId is not Guid quotationId) { Message = "No hay una cotización aceptada para cargar."; return; }
        PurchaseDraftFromQuotationResult draft = await _convertQuotation.ExecuteAsync(quotationId, cancellationToken).ConfigureAwait(true);
        _supplierQuotationId = draft.SupplierQuotationId;
        PurchaseDate = draft.EffectiveDate.ToDateTime(TimeOnly.MinValue);
        PaymentTerms = draft.PaymentTerms ?? string.Empty;
        Observations = draft.Observations ?? string.Empty;
        Items.Clear(); foreach (PurchaseDraftItem item in draft.Items) Items.Add(item); RecalculateTotals();
        Message = "Borrador de compra cargado desde la cotización. Confirma la compra para recibir inventario.";
    }

    private async Task LoadCatalogAsync(Guid supplierId)
    {
        if (_catalog is null) return;
        IReadOnlyList<SupplierCatalogItemResult> items = await _catalog.ExecuteAsync(supplierId).ConfigureAwait(true);
        CatalogItems.Clear(); foreach (SupplierCatalogItemResult item in items) CatalogItems.Add(item);
        Message = CatalogItems.Count == 0 ? "El proveedor no tiene productos activos en su catálogo." : "Selecciona una presentación de compra del catálogo.";
    }

    private void AddDraftItem()
    {
        if (SelectedCatalogItem is null) return;
        Items.Add(new PurchaseDraftItem(SelectedCatalogItem.ProductId, SelectedCatalogItem.ProductName, SelectedCatalogItem.SupplierSku ?? SelectedCatalogItem.ProductReference, SelectedCatalogItem.PurchaseUnitName, SelectedCatalogItem.UnitsPerPurchaseUnit, SelectedCatalogItem.PurchaseUnitCost, DraftQuantity));
        DraftQuantity = 1m; SelectedCatalogItem = null; RecalculateTotals();
    }

    private void RecalculateTotals() { OnPropertyChanged(nameof(Subtotal)); OnPropertyChanged(nameof(Iva)); OnPropertyChanged(nameof(Total)); OnPropertyChanged(nameof(ItemCountLabel)); }
    private static string? Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
