using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using POS.Application.DTOs.Purchases;
using POS.Application.UseCases.Purchases;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

/// <summary>
/// Editable draft line of a new purchase. Quantity and confirmed cost can be changed
/// directly from the cart; totals and inventory units recalculate immediately.
/// </summary>
public sealed class NewPurchaseLineViewModel : ViewModelBase
{
    public const decimal MaxQuantity = 999_999m;
    public const decimal MaxUnitCost = 999_999_999m;

    private decimal _quantity;
    private decimal _confirmedCost;

    public NewPurchaseLineViewModel(SupplierCatalogItemResult catalogItem, decimal quantity, decimal confirmedCost)
    {
        ArgumentNullException.ThrowIfNull(catalogItem);
        if (!catalogItem.IsEligibleForPurchase) throw new ArgumentException("Only eligible catalog items can become purchase lines.", nameof(catalogItem));
        CatalogItem = catalogItem;
        _quantity = Clamp(quantity, MaxQuantity);
        _confirmedCost = Clamp(confirmedCost, MaxUnitCost);
    }

    public SupplierCatalogItemResult CatalogItem { get; }
    public Guid ProductId => CatalogItem.ProductId;
    public string ProductName => CatalogItem.ProductName;
    public string? Reference => CatalogItem.SupplierSku ?? CatalogItem.ProductReference;
    public string PurchaseUnitName => CatalogItem.PurchaseUnitName!;
    public decimal UnitsPerPurchaseUnit => CatalogItem.UnitsPerPurchaseUnit!.Value;
    public decimal CatalogUnitCost => CatalogItem.PurchaseUnitCost!.Value;

    public decimal Quantity
    {
        get => _quantity;
        set { if (SetProperty(ref _quantity, Clamp(value, MaxQuantity))) NotifyLineChanged(); }
    }

    public decimal ConfirmedCost
    {
        get => _confirmedCost;
        set { if (SetProperty(ref _confirmedCost, Clamp(value, MaxUnitCost))) NotifyLineChanged(); }
    }

    public decimal UnitsReceived => Quantity * UnitsPerPurchaseUnit;
    public decimal Subtotal => decimal.Round(Quantity * ConfirmedCost, 2, MidpointRounding.AwayFromZero);
    public bool IsValid => Quantity > 0m && ConfirmedCost > 0m;

    public RegisterPurchaseItemRequest ToRequest()
        => new(ProductId, Quantity, ConfirmedCost == CatalogUnitCost ? null : ConfirmedCost);

    private void NotifyLineChanged()
    {
        OnPropertyChanged(nameof(UnitsReceived));
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(IsValid));
    }

    private static decimal Clamp(decimal value, decimal max) => Math.Clamp(value, 0m, max);
}

/// <summary>
/// Dedicated state for the "Nueva compra" flow: supplier selection, eligible-catalog
/// autocomplete, editable draft cart, validated step navigation, and final review.
/// All business rules stay in RegisterPurchaseUseCase; this only gates the UI flow.
/// </summary>
public sealed class NewPurchaseViewModel : ViewModelBase
{
    public const decimal MaxVatRate = 100m;

    private readonly SearchSuppliersUseCase _searchSuppliers;
    private readonly RegisterPurchaseUseCase _registerPurchase;
    private readonly Func<Guid> _currentOperatorIdProvider;
    private readonly GetEligibleSupplierCatalogUseCase? _eligibleCatalog;
    private readonly RelayCommand _goToStepCommand;
    private readonly RelayCommand _addDraftItemCommand;
    private readonly RelayCommand _removeDraftItemCommand;
    private readonly RelayCommand _confirmPurchaseCommand;
    private readonly RelayCommand _changeSupplierCommand;

    private List<SupplierCatalogItemResult> _allEligibleCatalogItems = [];
    private SupplierSearchResult? _selectedSupplier;
    private SupplierCatalogItemResult? _selectedCatalogItem;
    private string _supplierSearchText = string.Empty;
    private string _catalogSearchText = string.Empty;
    private decimal _draftQuantity = 1m;
    private decimal _draftCostOverride;
    private bool _useDraftCostOverride;
    private string _lastGeneratedInvoiceNumber = string.Empty;
    private string _purchaseInvoiceNumber = string.Empty;
    private DateTime? _purchaseDate = DateTime.Today;
    private string _paymentMethod = "Contado";
    private string _paymentTerms = string.Empty;
    private string _observations = string.Empty;
    private decimal _vatRate = 19m;
    private int _currentStep = 1;
    private bool _hasNoCatalogMatches;
    private string _message = "Selecciona un proveedor activo para iniciar la compra.";

    public NewPurchaseViewModel(
        SearchSuppliersUseCase searchSuppliers,
        RegisterPurchaseUseCase registerPurchase,
        Func<Guid> currentOperatorIdProvider,
        GetEligibleSupplierCatalogUseCase? eligibleCatalog = null)
    {
        _searchSuppliers = searchSuppliers ?? throw new ArgumentNullException(nameof(searchSuppliers));
        _registerPurchase = registerPurchase ?? throw new ArgumentNullException(nameof(registerPurchase));
        _currentOperatorIdProvider = currentOperatorIdProvider ?? throw new ArgumentNullException(nameof(currentOperatorIdProvider));
        _eligibleCatalog = eligibleCatalog;

        Items.CollectionChanged += OnItemsChanged;

        _goToStepCommand = new RelayCommand(
            parameter => { if (TryParseStep(parameter, out int step) && CanGoToStep(step)) CurrentStep = step; },
            parameter => TryParseStep(parameter, out int step) && CanGoToStep(step));
        GoToStepCommand = _goToStepCommand;
        _changeSupplierCommand = new RelayCommand(_ => ChangeSupplier(), _ => SelectedSupplier is not null);
        ChangeSupplierCommand = _changeSupplierCommand;
        _addDraftItemCommand = new RelayCommand(_ => AddSelectedCatalogItem(),
            _ => SelectedCatalogItem is { IsEligibleForPurchase: true } item
                && !Items.Any(line => line.ProductId == item.ProductId)
                && DraftQuantity > 0m
                && (!UseDraftCostOverride || DraftCostOverride > 0m));
        AddDraftItemCommand = _addDraftItemCommand;
        _removeDraftItemCommand = new RelayCommand(parameter =>
        {
            if (parameter is NewPurchaseLineViewModel line) Items.Remove(line);
        }, parameter => parameter is NewPurchaseLineViewModel);
        RemoveDraftItemCommand = _removeDraftItemCommand;
        _confirmPurchaseCommand = new RelayCommand(
            _ => RunSafe(ConfirmPurchaseAsync(), ex => Message = $"Error al registrar compra: {ex.Message}"),
            _ => CanConfirm);
        ConfirmPurchaseCommand = _confirmPurchaseCommand;
    }

    public ObservableCollection<SupplierSearchResult> Suppliers { get; } = [];
    public ObservableCollection<SupplierCatalogItemResult> CatalogSuggestions { get; } = [];
    public ObservableCollection<NewPurchaseLineViewModel> Items { get; } = [];

    /// <summary>System-generated reference for the most recently completed purchase.</summary>
    public string LastGeneratedInvoiceNumber { get => _lastGeneratedInvoiceNumber; private set => SetProperty(ref _lastGeneratedInvoiceNumber, value); }

    public ICommand GoToStepCommand { get; }
    public ICommand ChangeSupplierCommand { get; }
    public ICommand AddDraftItemCommand { get; }
    public ICommand RemoveDraftItemCommand { get; }
    public ICommand ConfirmPurchaseCommand { get; }

    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    // --- Step 1: supplier selection ---

    public string SupplierSearchText
    {
        get => _supplierSearchText;
        set { if (SetProperty(ref _supplierSearchText, value)) RunSafe(LoadAsync(), ex => Message = $"Error al cargar proveedores: {ex.Message}"); }
    }

    public SupplierSearchResult? SelectedSupplier
    {
        get => _selectedSupplier;
        set
        {
            if (!SetProperty(ref _selectedSupplier, value)) return;
            OnPropertyChanged(nameof(HasSupplier));
            OnPropertyChanged(nameof(SupplierContextLabel));
            ResetDraftState();
            _allEligibleCatalogItems = [];
            ApplyCatalogFilter();
            if (value is not null)
            {
                RunSafe(LoadEligibleCatalogAsync(value.Id), ex => Message = $"Error al cargar catálogo: {ex.Message}");
                Message = $"Proveedor seleccionado: {value.Name}.";
            }
            RefreshFlowState();
        }
    }

    public bool HasSupplier => SelectedSupplier is not null;
    public string SupplierContextLabel => SelectedSupplier is null ? string.Empty : $"{SelectedSupplier.Name} · NIT {SelectedSupplier.TaxId}";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<SupplierSearchResult> suppliers = await _searchSuppliers.ExecuteAsync(new SupplierSearchRequest(SupplierSearchText, false), cancellationToken).ConfigureAwait(true);
            Suppliers.Clear();
            foreach (SupplierSearchResult supplier in suppliers) Suppliers.Add(supplier);
            if (Suppliers.Count == 0) Message = "No hay proveedores activos. Crea o activa uno en 'Proveedores y catálogo'.";
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar proveedores: {ex.Message}";
        }
    }

    // --- Step 2: eligible-catalog autocomplete + draft cart ---

    public string CatalogSearchText
    {
        get => _catalogSearchText;
        set { if (SetProperty(ref _catalogSearchText, value)) ApplyCatalogFilter(); }
    }

    public SupplierCatalogItemResult? SelectedCatalogItem
    {
        get => _selectedCatalogItem;
        set
        {
            if (!SetProperty(ref _selectedCatalogItem, value)) return;
            NotifyDraftPreviewChanged();
            RefreshFlowState();
        }
    }

    public bool HasNoCatalogMatches { get => _hasNoCatalogMatches; private set => SetProperty(ref _hasNoCatalogMatches, value); }

    public decimal DraftQuantity
    {
        get => _draftQuantity;
        set { if (SetProperty(ref _draftQuantity, Math.Clamp(value, 0m, NewPurchaseLineViewModel.MaxQuantity))) { NotifyDraftPreviewChanged(); RefreshFlowState(); } }
    }

    public bool UseDraftCostOverride
    {
        get => _useDraftCostOverride;
        set { if (SetProperty(ref _useDraftCostOverride, value)) { NotifyDraftPreviewChanged(); RefreshFlowState(); } }
    }

    public decimal DraftCostOverride
    {
        get => _draftCostOverride;
        set { if (SetProperty(ref _draftCostOverride, Math.Clamp(value, 0m, NewPurchaseLineViewModel.MaxUnitCost))) { NotifyDraftPreviewChanged(); RefreshFlowState(); } }
    }

    public decimal DraftEffectiveUnitCost => UseDraftCostOverride ? DraftCostOverride : SelectedCatalogItem?.PurchaseUnitCost ?? 0m;
    public decimal DraftUnitsPreview => (SelectedCatalogItem?.UnitsPerPurchaseUnit ?? 0m) * DraftQuantity;
    public decimal DraftSubtotalPreview => decimal.Round(DraftQuantity * DraftEffectiveUnitCost, 2, MidpointRounding.AwayFromZero);

    /// <summary>Moves the highlighted catalog suggestion up/down; used by arrow keys from the search box.</summary>
    public void MoveCatalogSelection(int delta)
    {
        if (CatalogSuggestions.Count == 0) return;
        int currentIndex = SelectedCatalogItem is null ? -1 : CatalogSuggestions.IndexOf(SelectedCatalogItem);
        int nextIndex = Math.Clamp(currentIndex + delta, 0, CatalogSuggestions.Count - 1);
        SelectedCatalogItem = CatalogSuggestions[nextIndex];
    }

    /// <summary>Adds the highlighted suggestion as a draft line (Enter, double click, Agregar button).</summary>
    public bool AddSelectedCatalogItem()
    {
        if (SelectedCatalogItem is not { IsEligibleForPurchase: true } item)
        {
            Message = "Selecciona un producto del catálogo elegible del proveedor.";
            return false;
        }
        if (DraftQuantity <= 0m) { Message = "La cantidad debe ser mayor que cero."; return false; }
        if (UseDraftCostOverride && DraftCostOverride <= 0m) { Message = "El costo confirmado debe ser mayor que cero."; return false; }
        if (Items.Any(line => line.ProductId == item.ProductId))
        {
            Message = $"'{item.ProductName}' ya está en la compra. Edita la línea existente.";
            return false;
        }

        decimal confirmedCost = UseDraftCostOverride ? DraftCostOverride : item.PurchaseUnitCost!.Value;
        var line = new NewPurchaseLineViewModel(item, DraftQuantity, confirmedCost);
        Items.Add(line);
        RefreshFlowState();
        DraftQuantity = 1m;
        DraftCostOverride = 0m;
        UseDraftCostOverride = false;
        CatalogSearchText = string.Empty;
        Message = $"'{item.ProductName}' agregado a la compra.";
        return true;
    }

    // --- Totals ---

    public decimal Subtotal => Items.Sum(line => line.Subtotal);
    public decimal Iva => decimal.Round(Subtotal * VatRate / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal Total => Subtotal + Iva;
    public string ItemCountLabel => $"{Items.Count} producto(s)";

    public decimal VatRate
    {
        get => _vatRate;
        set { if (SetProperty(ref _vatRate, Math.Clamp(value, 0m, MaxVatRate))) { RecalculateTotals(); RefreshFlowState(); } }
    }

    // --- Step 3: review header ---

    /// <summary>Manual invoice number the operator types in from the supplier's paper invoice, if any. Left blank, the system generates a reference instead.</summary>
    public string PurchaseInvoiceNumber
    {
        get => _purchaseInvoiceNumber;
        set { if (SetProperty(ref _purchaseInvoiceNumber, value)) RefreshFlowState(); }
    }

    public DateTime? PurchaseDate
    {
        get => _purchaseDate;
        set { if (SetProperty(ref _purchaseDate, value)) RefreshFlowState(); }
    }

    public string PaymentMethod
    {
        get => _paymentMethod;
        set { if (SetProperty(ref _paymentMethod, value)) { OnPropertyChanged(nameof(IsCreditPayment)); RefreshFlowState(); } }
    }

    public bool IsCreditPayment => PaymentMethod == "Crédito";

    public string PaymentTerms
    {
        get => _paymentTerms;
        set { if (SetProperty(ref _paymentTerms, value)) RefreshFlowState(); }
    }

    public string Observations { get => _observations; set => SetProperty(ref _observations, value); }

    // --- Validated step navigation ---

    public int CurrentStep
    {
        get => _currentStep;
        private set
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

    public bool CanGoToStep(int step) => step switch
    {
        1 => true,
        2 => SelectedSupplier is { IsActive: true },
        3 => SelectedSupplier is { IsActive: true } && Items.Count > 0 && Items.All(line => line.IsValid),
        _ => false
    };

    // --- Confirm as a reviewed, ViewModel-gated operation ---

    public bool CanConfirm => ConfirmBlockReason is null;

    /// <summary>First rule blocking confirmation, in business language; null when the purchase can be confirmed.</summary>
    public string? ConfirmBlockReason
    {
        get
        {
            if (SelectedSupplier is not { IsActive: true }) return "Selecciona un proveedor activo.";
            if (Items.Count == 0) return "Agrega al menos un producto del catálogo elegible.";
            if (Items.Any(line => !line.IsValid)) return "Hay líneas con cantidad o costo en cero.";
            if (PurchaseDate is null) return "Selecciona la fecha de la compra.";
            if (IsCreditPayment && string.IsNullOrWhiteSpace(PaymentTerms)) return "Indica el plazo para compras a crédito.";
            return null;
        }
    }

    public async Task ConfirmPurchaseAsync(CancellationToken cancellationToken = default)
    {
        if (!CanConfirm) { Message = ConfirmBlockReason!; return; }
        Guid operatorId = _currentOperatorIdProvider();
        if (operatorId == Guid.Empty) { Message = "No se pudo identificar el operador."; return; }

        var request = new RegisterPurchaseRequest(
            SelectedSupplier!.Id,
            operatorId,
            DateOnly.FromDateTime(PurchaseDate!.Value),
            Normalize(PurchaseInvoiceNumber),
            Normalize(PaymentTerms),
            PaymentMethod,
            Normalize(Observations),
            VatRate,
            Items.Select(line => line.ToRequest()).ToList());
        RegisterPurchaseResult result = await _registerPurchase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

        Items.Clear();
        LastGeneratedInvoiceNumber = result.InvoiceNumber ?? string.Empty;
        PurchaseInvoiceNumber = string.Empty;
        PurchaseDate = DateTime.Today;
        PaymentMethod = "Contado";
        PaymentTerms = string.Empty;
        Observations = string.Empty;
        CurrentStep = 1;
        Message = string.IsNullOrWhiteSpace(LastGeneratedInvoiceNumber)
            ? $"Compra registrada. Total: {result.TotalCost:N2}."
            : $"Compra registrada con referencia {LastGeneratedInvoiceNumber}. Total: {result.TotalCost:N2}.";
    }

    // --- Internals ---

    private void ChangeSupplier()
    {
        SelectedSupplier = null;
        PurchaseInvoiceNumber = string.Empty;
        CurrentStep = 1;
        Message = "Selecciona un proveedor activo para iniciar la compra.";
    }

    private async Task LoadEligibleCatalogAsync(Guid supplierId, CancellationToken cancellationToken = default)
    {
        if (_eligibleCatalog is null) return;
        _allEligibleCatalogItems = (await _eligibleCatalog.ExecuteAsync(supplierId, cancellationToken).ConfigureAwait(true)).ToList();
        ApplyCatalogFilter();
        if (_allEligibleCatalogItems.Count == 0)
            Message = "Este proveedor no tiene productos activos y configurados. Configura su catálogo en 'Proveedores y catálogo'.";
    }

    private void ApplyCatalogFilter()
    {
        CatalogSuggestions.Clear();
        string filterText = CatalogSearchText;
        IEnumerable<SupplierCatalogItemResult> source = string.IsNullOrWhiteSpace(filterText)
            ? _allEligibleCatalogItems
            : _allEligibleCatalogItems.Where(item =>
                item.ProductName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
                (item.ProductReference?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (item.SupplierSku?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false));
        foreach (SupplierCatalogItemResult item in source) CatalogSuggestions.Add(item);

        HasNoCatalogMatches = !string.IsNullOrWhiteSpace(filterText) && CatalogSuggestions.Count == 0;
        if (SelectedCatalogItem is not null && !CatalogSuggestions.Contains(SelectedCatalogItem)) SelectedCatalogItem = null;
        if (SelectedCatalogItem is null && !string.IsNullOrWhiteSpace(filterText) && CatalogSuggestions.Count > 0)
            SelectedCatalogItem = CatalogSuggestions[0];
    }

    private void ResetDraftState()
    {
        Items.Clear();
        _catalogSearchText = string.Empty;
        OnPropertyChanged(nameof(CatalogSearchText));
        SelectedCatalogItem = null;
        DraftQuantity = 1m;
        DraftCostOverride = 0m;
        UseDraftCostOverride = false;
        HasNoCatalogMatches = false;
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null) foreach (NewPurchaseLineViewModel line in e.OldItems) line.PropertyChanged -= OnLineChanged;
        if (e.NewItems is not null) foreach (NewPurchaseLineViewModel line in e.NewItems) line.PropertyChanged += OnLineChanged;
        RecalculateTotals();
        RefreshFlowState();
    }

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NewPurchaseLineViewModel.Subtotal) or nameof(NewPurchaseLineViewModel.IsValid))
        {
            RecalculateTotals();
            RefreshFlowState();
        }
    }

    private void NotifyDraftPreviewChanged()
    {
        OnPropertyChanged(nameof(DraftEffectiveUnitCost));
        OnPropertyChanged(nameof(DraftUnitsPreview));
        OnPropertyChanged(nameof(DraftSubtotalPreview));
    }

    private void RecalculateTotals()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(Iva));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(ItemCountLabel));
    }

    private void RefreshFlowState()
    {
        OnPropertyChanged(nameof(CanConfirm));
        OnPropertyChanged(nameof(ConfirmBlockReason));
        _goToStepCommand.RaiseCanExecuteChanged();
        _addDraftItemCommand.RaiseCanExecuteChanged();
        _confirmPurchaseCommand.RaiseCanExecuteChanged();
        _changeSupplierCommand.RaiseCanExecuteChanged();
    }

    private static bool TryParseStep(object? parameter, out int step)
    {
        switch (parameter)
        {
            case int value: step = value; return true;
            case string text when int.TryParse(text, out int parsed): step = parsed; return true;
            default: step = 0; return false;
        }
    }

    private static string? Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
