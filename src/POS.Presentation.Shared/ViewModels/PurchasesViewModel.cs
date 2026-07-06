using System.Collections.ObjectModel;
using System.Globalization;
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
    private readonly RelayCommand _addDraftItemCommand;
    private SupplierSearchResult? _selectedSupplier;
    private string _supplierName = string.Empty;
    private string _supplierTaxId = string.Empty;
    private string _supplierPhone = string.Empty;
    private string _supplierEmail = string.Empty;
    private string _supplierSearchText = string.Empty;
    private string _draftProductId = string.Empty;
    private decimal _draftQuantity = 1m;
    private decimal _draftTotalCost;
    private string _message = "Selecciona un proveedor o registra uno nuevo antes de confirmar una compra.";
    private bool _isNewSupplierFormOpen;
    private string _purchaseInvoiceNumber = string.Empty;
    private DateTime? _purchaseDate = DateTime.Today;
    private string _paymentMethod = "Contado";

    public PurchasesViewModel(
        SaveSupplierUseCase saveSupplier,
        SearchSuppliersUseCase searchSuppliers,
        RegisterPurchaseUseCase registerPurchase,
        Func<Guid> currentOperatorIdProvider)
    {
        _saveSupplier = saveSupplier;
        _searchSuppliers = searchSuppliers;
        _registerPurchase = registerPurchase;
        _currentOperatorIdProvider = currentOperatorIdProvider;
        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar proveedores: {ex.Message}"));
        SaveSupplierCommand = new RelayCommand(_ => RunSafe(SaveSupplierAsync(), ex => Message = $"Error al guardar proveedor: {ex.Message}"));
        RegisterPurchaseCommand = new RelayCommand(_ => RunSafe(RegisterPurchaseAsync(), ex => Message = $"Error al registrar compra: {ex.Message}"));
        _addDraftItemCommand = new RelayCommand(_ => AddDraftItem(), _ => CanAddDraftItem());
        AddDraftItemCommand = _addDraftItemCommand;
        RemoveDraftItemCommand = new RelayCommand(param =>
        {
            if (param is PurchaseDraftItem item)
            {
                Items.Remove(item);
                RecalculateTotals();
            }
        });
        ToggleNewSupplierCommand = new RelayCommand(_ => IsNewSupplierFormOpen = !IsNewSupplierFormOpen);
    }

    public ObservableCollection<SupplierSearchResult> Suppliers { get; } = [];

    public ObservableCollection<PurchaseDraftItem> Items { get; } = [];

    public ICommand LoadCommand { get; }

    public ICommand SaveSupplierCommand { get; }

    public ICommand RegisterPurchaseCommand { get; }

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
                OnPropertyChanged(nameof(SelectedSupplierChipKind));
            }
        }
    }

    public bool HasSelectedSupplier => _selectedSupplier is not null;

    public string SelectedSupplierChipLabel =>
        _selectedSupplier is not null ? _selectedSupplier.Name : "Selecciona un proveedor";

    public string SelectedSupplierChipKind =>
        _selectedSupplier is not null ? "primary" : "neutral";

    public string SupplierName
    {
        get => _supplierName;
        set => SetProperty(ref _supplierName, value);
    }

    public string SupplierTaxId
    {
        get => _supplierTaxId;
        set => SetProperty(ref _supplierTaxId, value);
    }

    public string SupplierPhone
    {
        get => _supplierPhone;
        set => SetProperty(ref _supplierPhone, value);
    }

    public string SupplierEmail
    {
        get => _supplierEmail;
        set => SetProperty(ref _supplierEmail, value);
    }

    public string SupplierSearchText
    {
        get => _supplierSearchText;
        set => SetProperty(ref _supplierSearchText, value);
    }

    public bool IsNewSupplierFormOpen
    {
        get => _isNewSupplierFormOpen;
        set => SetProperty(ref _isNewSupplierFormOpen, value);
    }

    // ── Purchase header (presentation-only) ──

    public string PurchaseInvoiceNumber
    {
        get => _purchaseInvoiceNumber;
        set => SetProperty(ref _purchaseInvoiceNumber, value);
    }

    public DateTime? PurchaseDate
    {
        get => _purchaseDate;
        set => SetProperty(ref _purchaseDate, value);
    }

    public string PaymentMethod
    {
        get => _paymentMethod;
        set => SetProperty(ref _paymentMethod, value);
    }

    public string DraftProductId
    {
        get => _draftProductId;
        set
        {
            if (SetProperty(ref _draftProductId, value))
            {
                _addDraftItemCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public decimal DraftQuantity
    {
        get => _draftQuantity;
        set
        {
            if (SetProperty(ref _draftQuantity, value))
            {
                _addDraftItemCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public decimal DraftTotalCost
    {
        get => _draftTotalCost;
        set
        {
            if (SetProperty(ref _draftTotalCost, value))
            {
                _addDraftItemCommand.RaiseCanExecuteChanged();
            }
        }
    }

    // ── Totals (presentation-only) ──

    public decimal Subtotal => Items.Sum(i => i.TotalCost);

    public decimal Iva => Math.Round(Subtotal * 0.19m);

    public decimal Total => Subtotal + Iva;

    public string ItemCountLabel => Items.Count == 0 ? "0 items" : $"{Items.Count} item{(Items.Count != 1 ? "s" : string.Empty)}";

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<SupplierSearchResult> suppliers = await _searchSuppliers.ExecuteAsync(new SupplierSearchRequest(SupplierSearchText), cancellationToken).ConfigureAwait(true);
            Suppliers.Clear();
            foreach (SupplierSearchResult supplier in suppliers)
            {
                Suppliers.Add(supplier);
            }

            Message = Suppliers.Count == 0
                ? "No hay proveedores activos para la búsqueda. Registra uno para continuar."
                : "Selecciona un proveedor activo para registrar la compra.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar proveedores: {ex.Message}";
        }
    }

    public async Task SaveSupplierAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _saveSupplier.ExecuteAsync(new SaveSupplierRequest(null, SupplierName, SupplierTaxId, Normalize(SupplierPhone), Normalize(SupplierEmail), true), cancellationToken).ConfigureAwait(true);
            SupplierName = string.Empty;
            SupplierTaxId = string.Empty;
            SupplierPhone = string.Empty;
            SupplierEmail = string.Empty;
            IsNewSupplierFormOpen = false;
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            Message = "Proveedor guardado";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"Error al guardar proveedor: {ex.Message}";
        }
    }

    public async Task RegisterPurchaseAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSupplier is null)
        {
            Message = "Selecciona un proveedor";
            return;
        }

        if (Items.Count == 0)
        {
            Message = "Agrega al menos un producto";
            return;
        }

        try
        {
            Guid operatorId = _currentOperatorIdProvider();
            if (operatorId == Guid.Empty)
            {
                Message = "No se pudo identificar el operador para auditar la compra.";
                return;
            }

            RegisterPurchaseResult result = await _registerPurchase.ExecuteAsync(new RegisterPurchaseRequest(
                SelectedSupplier.Id,
                operatorId,
                Items.Select(item => new RegisterPurchaseItemRequest(item.ProductId, item.Quantity, item.TotalCost)).ToList()), cancellationToken).ConfigureAwait(true);

            var supplierName = SelectedSupplier.Name;
            Items.Clear();
            RecalculateTotals();
            PurchaseInvoiceNumber = string.Empty;
            PurchaseDate = DateTime.Today;
            PaymentMethod = "Contado";
            Message = $"Compra registrada · Proveedor: {supplierName}";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"Error al registrar compra: {ex.Message}";
        }
    }

    private void AddDraftItem()
    {
        if (!Guid.TryParse(DraftProductId, out Guid productId))
        {
            Message = "El identificador del producto no es válido.";
            return;
        }

        Items.Add(new PurchaseDraftItem(productId, DraftQuantity, DraftTotalCost));
        DraftProductId = string.Empty;
        DraftQuantity = 1m;
        DraftTotalCost = 0m;
        RecalculateTotals();
        Message = "Producto agregado a la compra.";
    }

    private bool CanAddDraftItem() => !string.IsNullOrWhiteSpace(DraftProductId) && DraftQuantity > 0m && DraftTotalCost >= 0m;

    private void RecalculateTotals()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(Iva));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(ItemCountLabel));
    }

    private static string? Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
