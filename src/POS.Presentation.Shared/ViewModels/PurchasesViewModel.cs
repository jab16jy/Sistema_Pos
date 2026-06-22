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
        LoadCommand = new RelayCommand(async _ => await LoadAsync().ConfigureAwait(true));
        SaveSupplierCommand = new RelayCommand(async _ => await SaveSupplierAsync().ConfigureAwait(true));
        RegisterPurchaseCommand = new RelayCommand(async _ => await RegisterPurchaseAsync().ConfigureAwait(true));
        _addDraftItemCommand = new RelayCommand(_ => AddDraftItem(), _ => CanAddDraftItem());
        AddDraftItemCommand = _addDraftItemCommand;
    }

    public ObservableCollection<SupplierSearchResult> Suppliers { get; } = [];

    public ObservableCollection<PurchaseDraftItem> Items { get; } = [];

    public ICommand LoadCommand { get; }

    public ICommand SaveSupplierCommand { get; }

    public ICommand RegisterPurchaseCommand { get; }

    public ICommand AddDraftItemCommand { get; }

    public SupplierSearchResult? SelectedSupplier
    {
        get => _selectedSupplier;
        set => SetProperty(ref _selectedSupplier, value);
    }

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

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
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

    public async Task SaveSupplierAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _saveSupplier.ExecuteAsync(new SaveSupplierRequest(null, SupplierName, SupplierTaxId, Normalize(SupplierPhone), Normalize(SupplierEmail), true), cancellationToken).ConfigureAwait(true);
            SupplierName = string.Empty;
            SupplierTaxId = string.Empty;
            SupplierPhone = string.Empty;
            SupplierEmail = string.Empty;
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            Message = "Proveedor guardado. Ya puedes seleccionarlo para una compra.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Message = ex.Message;
        }
    }

    public async Task RegisterPurchaseAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSupplier is null)
        {
            Message = "Selecciona un proveedor antes de registrar la compra.";
            return;
        }

        if (Items.Count == 0)
        {
            Message = "Agrega al menos un producto a la compra.";
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
            Items.Clear();
            Message = $"Compra registrada por {result.TotalCost.ToString("N0", CultureInfo.GetCultureInfo("es-CO"))}. Inventario actualizado automáticamente.";
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Message = ex.Message;
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
        Message = "Producto agregado a la compra.";
    }

    private bool CanAddDraftItem() => !string.IsNullOrWhiteSpace(DraftProductId) && DraftQuantity > 0m && DraftTotalCost >= 0m;

    private static string? Normalize(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
