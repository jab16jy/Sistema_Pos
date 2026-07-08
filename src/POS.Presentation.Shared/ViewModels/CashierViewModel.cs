using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Customers;
using POS.Application.DTOs.Loyalty;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Sales;
using POS.Application.Interfaces;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Invoices;
using POS.Application.UseCases.Loyalty;
using POS.Application.UseCases.Sales;
using POS.Domain.Enums;
using POS.Presentation.Shared.Cashier;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class CashierViewModel : ViewModelBase
{
    private const int AutocompleteDelayMilliseconds = 180;
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");
    private readonly IProductLookupService _productLookupService;
    private readonly PreviewPaymentUseCase _previewPaymentUseCase;
    private readonly CreateSaleUseCase? _createSaleUseCase;
    private readonly RedeemLoyaltyPointsUseCase? _redeemLoyaltyPointsUseCase;
    private readonly ReprintReceiptUseCase? _printReceiptUseCase;
    private readonly SearchCustomersUseCase? _searchCustomersUseCase;
    private readonly RelayCommand _printReceiptYesCommand;
    private readonly RelayCommand _printReceiptNoCommand;
    private Guid? _pendingPrintSaleId;
    private readonly RelayCommand _addSelectedProductCommand;
    private readonly RelayCommand _appendReceivedAmountCommand;
    private readonly RelayCommand _backspaceReceivedAmountCommand;
    private readonly RelayCommand _setExactAmountCommand;
    private readonly RelayCommand _clearCartCommand;
    private readonly RelayCommand _editCartItemCommand;
    private readonly RelayCommand _removeCartItemCommand;
    private readonly RelayCommand _associateCustomerCommand;
    private readonly RelayCommand _holdSaleCommand;
    private readonly RelayCommand _confirmPrototypeSaleCommand;
    private readonly RelayCommand _clearCustomerCommand;
    private CancellationTokenSource? _autocompleteSearchCts;
    private string _searchText = string.Empty;
    private CashierProductSearchResultViewModel? _selectedProduct;
    private CashierCartItemViewModel? _selectedCartItem;
    private decimal _quantity = 1m;
    private PaymentMethodOption _selectedPaymentMethod;
    private decimal _receivedAmount;
    private decimal _deferredAmount;
    private string _message = "Busca o escanea un producto para iniciar la venta.";
    private bool _hasNoMatches;
    private bool _canConfirm;
    private decimal _changeAmount;
    private CustomerSearchResult? _selectedCustomer;
    private Guid? _customerId;
    private string _customerSearchText = string.Empty;
    private decimal _loyaltyDiscount;
    private string _bankOrPlatform = string.Empty;
    private int _loyaltyPointsToRedeem;
    private bool _isCustomerAssignmentOpen;
    private bool _confirmAfterCustomerAssignment;
    private bool _customerAssignmentReviewed;

    public CashierViewModel(IProductLookupService productLookupService)
        : this(productLookupService, new PreviewPaymentUseCase())
    {
    }

    public CashierViewModel(IProductLookupService productLookupService, PreviewPaymentUseCase previewPaymentUseCase)
        : this(productLookupService, previewPaymentUseCase, null, null, null)
    {
    }

    public CashierViewModel(IProductLookupService productLookupService, CreateSaleUseCase createSaleUseCase)
        : this(productLookupService, new PreviewPaymentUseCase(), createSaleUseCase, null, null)
    {
    }

    public CashierViewModel(
        IProductLookupService productLookupService,
        PreviewPaymentUseCase previewPaymentUseCase,
        CreateSaleUseCase? createSaleUseCase)
        : this(productLookupService, previewPaymentUseCase, createSaleUseCase, null, null)
    {
    }

    public CashierViewModel(
        IProductLookupService productLookupService,
        PreviewPaymentUseCase previewPaymentUseCase,
        CreateSaleUseCase? createSaleUseCase,
        RedeemLoyaltyPointsUseCase? redeemLoyaltyPointsUseCase)
        : this(productLookupService, previewPaymentUseCase, createSaleUseCase, redeemLoyaltyPointsUseCase, null)
    {
    }

    public CashierViewModel(
        IProductLookupService productLookupService,
        PreviewPaymentUseCase previewPaymentUseCase,
        CreateSaleUseCase? createSaleUseCase,
        RedeemLoyaltyPointsUseCase? redeemLoyaltyPointsUseCase,
        ReprintReceiptUseCase? printReceiptUseCase,
        SearchCustomersUseCase? searchCustomersUseCase = null)
    {
        _productLookupService = productLookupService ?? throw new ArgumentNullException(nameof(productLookupService));
        _previewPaymentUseCase = previewPaymentUseCase ?? throw new ArgumentNullException(nameof(previewPaymentUseCase));
        _createSaleUseCase = createSaleUseCase;
        _redeemLoyaltyPointsUseCase = redeemLoyaltyPointsUseCase;
        _printReceiptUseCase = printReceiptUseCase;
        _searchCustomersUseCase = searchCustomersUseCase;

        PaymentMethods = new ObservableCollection<PaymentMethodOption>
        {
            new(PaymentMethod.Cash, "Efectivo"),
            new(PaymentMethod.DebitCard, "Débito"),
            new(PaymentMethod.CreditCard, "Crédito"),
            new(PaymentMethod.Transfer, "Transferencia")
        };
        _selectedPaymentMethod = PaymentMethods[0];

        SearchResults = [];
        CartItems = [];
        CustomerSearchResults = [];
        CartItems.CollectionChanged += OnCartItemsChanged;

        SearchCommand = new RelayCommand(_ => RunSafe(SearchAsync(), ex => Message = $"Error al buscar productos: {ex.Message}"));
        _addSelectedProductCommand = new RelayCommand(_ => TryAddSelectedProduct(), _ => CanAddSelectedProduct);
        _appendReceivedAmountCommand = new RelayCommand(AppendReceivedAmount);
        _backspaceReceivedAmountCommand = new RelayCommand(_ => BackspaceReceivedAmount());
        _setExactAmountCommand = new RelayCommand(_ => SetExactReceivedAmount(), _ => TotalAmount > 0m);
        _clearCartCommand = new RelayCommand(_ => ClearCart(), _ => CartItems.Count > 0);
        _editCartItemCommand = new RelayCommand(EditCartItem, parameter => parameter is CashierCartItemViewModel);
        _removeCartItemCommand = new RelayCommand(RemoveCartItem, parameter => parameter is CashierCartItemViewModel);
        _associateCustomerCommand = new RelayCommand(_ => AssociateCustomer());
        _holdSaleCommand = new RelayCommand(_ => HoldSale(), _ => HasCartItems);
        _confirmPrototypeSaleCommand = new RelayCommand(_ => ConfirmPrototypeSale());
        _printReceiptYesCommand = new RelayCommand(_ => RunSafe(PrintPendingReceiptAsync(), ex => Message = $"Error al imprimir el recibo: {ex.Message}"), _ => IsAwaitingPrintChoice);
        _printReceiptNoCommand = new RelayCommand(_ => DeclinePendingReceipt(), _ => IsAwaitingPrintChoice);
        AddSelectedProductCommand = _addSelectedProductCommand;
        AppendReceivedAmountCommand = _appendReceivedAmountCommand;
        BackspaceReceivedAmountCommand = _backspaceReceivedAmountCommand;
        SetExactAmountCommand = _setExactAmountCommand;
        ClearCartCommand = _clearCartCommand;
        EditCartItemCommand = _editCartItemCommand;
        RemoveCartItemCommand = _removeCartItemCommand;
        AssociateCustomerCommand = _associateCustomerCommand;
        HoldSaleCommand = _holdSaleCommand;
        ConfirmPrototypeSaleCommand = _confirmPrototypeSaleCommand;
        ApplyRedemptionCommand = new RelayCommand(_ => RunSafe(ApplyRedemptionAsync(), ex => Message = $"Error al aplicar puntos: {ex.Message}"));
        PrintReceiptYesCommand = _printReceiptYesCommand;
        PrintReceiptNoCommand = _printReceiptNoCommand;
        SearchCustomerCommand = new RelayCommand(_ => RunSafe(SearchCustomersAsync(), ex => Message = $"Error al buscar clientes: {ex.Message}"));
        SelectCustomerCommand = new RelayCommand(parameter => RunSafe(SelectCustomerFromCommandAsync(parameter), ex => Message = $"Error al asignar cliente: {ex.Message}"), parameter => parameter is CustomerSearchResult);
        ContinueWithoutCustomerCommand = new RelayCommand(_ => RunSafe(ContinueWithoutCustomerAsync(), ex => Message = $"Error al continuar sin cliente: {ex.Message}"));
        _clearCustomerCommand = new RelayCommand(_ => ClearCustomerAssignment(), _ => CustomerId is not null || SelectedCustomer is not null);
        ClearCustomerCommand = _clearCustomerCommand;
        CloseCustomerAssignmentCommand = new RelayCommand(_ => CloseCustomerAssignment());
    }

    public ObservableCollection<CashierProductSearchResultViewModel> SearchResults { get; }

    public ObservableCollection<CashierCartItemViewModel> CartItems { get; }

    public ObservableCollection<PaymentMethodOption> PaymentMethods { get; }

    public ObservableCollection<CustomerSearchResult> CustomerSearchResults { get; }

    public ICommand SearchCommand { get; }

    public ICommand AddSelectedProductCommand { get; }

    public ICommand AppendReceivedAmountCommand { get; }

    public ICommand BackspaceReceivedAmountCommand { get; }

    public ICommand SetExactAmountCommand { get; }

    public ICommand ClearCartCommand { get; }

    public ICommand EditCartItemCommand { get; }

    public ICommand RemoveCartItemCommand { get; }

    public ICommand AssociateCustomerCommand { get; }

    public ICommand HoldSaleCommand { get; }

    public ICommand ConfirmPrototypeSaleCommand { get; }

    public ICommand SearchCustomerCommand { get; }

    public ICommand SelectCustomerCommand { get; }

    public ICommand ContinueWithoutCustomerCommand { get; }

    public ICommand ClearCustomerCommand { get; }

    public ICommand CloseCustomerAssignmentCommand { get; }

    public Guid CashierUserId { get; set; }

    public Guid? CustomerId
    {
        get => _customerId;
        set
        {
            if (SetProperty(ref _customerId, value))
            {
                OnPropertyChanged(nameof(HasAssignedCustomer));
                OnPropertyChanged(nameof(AssignedCustomerLabel));
                _clearCustomerCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public CustomerSearchResult? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                CustomerId = value?.Id;
                OnPropertyChanged(nameof(HasAssignedCustomer));
                OnPropertyChanged(nameof(AssignedCustomerLabel));
                _clearCustomerCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasAssignedCustomer => CustomerId.HasValue;

    public string AssignedCustomerLabel => SelectedCustomer is not null
        ? $"{SelectedCustomer.FullName} · ID {SelectedCustomer.Identification}"
        : CustomerId.HasValue ? "Cliente asignado" : "Sin cliente asignado";

    public string CustomerSearchText
    {
        get => _customerSearchText;
        set => SetProperty(ref _customerSearchText, value ?? string.Empty);
    }

    public bool IsCustomerAssignmentOpen
    {
        get => _isCustomerAssignmentOpen;
        private set => SetProperty(ref _isCustomerAssignmentOpen, value);
    }

    public decimal LoyaltyDiscount
    {
        get => _loyaltyDiscount;
        private set => SetProperty(ref _loyaltyDiscount, value);
    }

    public string BankOrPlatform
    {
        get => _bankOrPlatform;
        set => SetProperty(ref _bankOrPlatform, value ?? string.Empty);
    }

    public int LoyaltyPointsToRedeem
    {
        get => _loyaltyPointsToRedeem;
        set => SetProperty(ref _loyaltyPointsToRedeem, value);
    }

    public ICommand ApplyRedemptionCommand { get; }

    public ICommand PrintReceiptYesCommand { get; }

    public ICommand PrintReceiptNoCommand { get; }

    /// <summary>True right after a sale is confirmed, while the cashier decides whether to print the thermal receipt.</summary>
    public bool IsAwaitingPrintChoice => _pendingPrintSaleId is not null;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                StartAutocompleteSearch();
            }
        }
    }

    public CashierProductSearchResultViewModel? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                UpdateCommandState();
            }
        }
    }

    public CashierCartItemViewModel? SelectedCartItem
    {
        get => _selectedCartItem;
        set => SetProperty(ref _selectedCartItem, value);
    }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            decimal normalized = value < 1m ? 1m : decimal.Round(value, 2, MidpointRounding.AwayFromZero);
            if (SetProperty(ref _quantity, normalized))
            {
                UpdateCommandState();
            }
        }
    }

    public PaymentMethodOption SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set
        {
            if (value is null)
            {
                return;
            }

            if (SetProperty(ref _selectedPaymentMethod, value))
            {
                RefreshPaymentPreview();
            }
        }
    }

    public decimal ReceivedAmount
    {
        get => _receivedAmount;
        set
        {
            decimal normalized = value < 0m ? 0m : decimal.Round(value, 2, MidpointRounding.AwayFromZero);
            if (SetProperty(ref _receivedAmount, normalized))
            {
                RefreshPaymentPreview();
            }
        }
    }

    /// <summary>
    /// Amount to defer as a receivable balance.
    /// When DeferredAmount > 0, a CustomerId is required and
    /// CollectedAmount + DeferredAmount must equal TotalAmount for CanConfirm to be true.
    /// </summary>
    public decimal DeferredAmount
    {
        get => _deferredAmount;
        set
        {
            decimal normalized = value < 0m ? 0m : decimal.Round(value, 2, MidpointRounding.AwayFromZero);
            if (SetProperty(ref _deferredAmount, normalized))
            {
                RefreshPaymentPreview();
            }
        }
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public void ShowError(string message) => Message = message;

    public decimal TotalAmount => CartItems.Sum(item => item.Subtotal);

    public string TotalText => FormatCurrency(TotalAmount);

    public int CartLineCount => CartItems.Count;

    public decimal CartUnitCount => CartItems.Sum(item => item.Quantity);

    public string CartSummaryText => $"{CartLineCount} productos · {CartUnitCount:0.##} unidades";

    public string SubtotalSummaryText => $"Subtotal: {TotalText}";

    public bool HasCartItems => CartItems.Count > 0;

    public decimal ChangeAmount
    {
        get => _changeAmount;
        private set
        {
            if (SetProperty(ref _changeAmount, value))
            {
                OnPropertyChanged(nameof(ChangeText));
            }
        }
    }

    public string ChangeText => FormatCurrency(ChangeAmount);

    public bool IsPaymentShortfall => HasCartItems &&
        SelectedPaymentMethod.Method == PaymentMethod.Cash &&
        ReceivedAmount > 0m &&
        ReceivedAmount < TotalAmount;

    public string PaymentBalanceLabel => IsPaymentShortfall ? "Falta" : "Cambio";

    public string PaymentBalanceText => IsPaymentShortfall ? FormatCurrency(TotalAmount - ReceivedAmount) : ChangeText;

    public bool CanConfirm
    {
        get => _canConfirm;
        private set
        {
            if (SetProperty(ref _canConfirm, value))
            {
                _confirmPrototypeSaleCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanAddSelectedProduct => SelectedProduct is not null && Quantity > 0m;

    /// <summary>True when the last search ran with text and found nothing — drives the "Sin coincidencias" hint.</summary>
    public bool HasNoMatches
    {
        get => _hasNoMatches;
        private set => SetProperty(ref _hasNoMatches, value);
    }

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            CancelAutocompleteSearch();
            await SearchProductsAsync(SearchText, updateMessage: true, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al buscar productos: {ex.Message}";
        }
    }

    public async Task HandleScannerInputAsync(CancellationToken cancellationToken = default)
    {
        string normalized = SearchText.Trim();
        if (normalized.Length == 0)
        {
            Message = "Escanea un código o escribe un producto para buscar.";
            return;
        }

        await SearchAsync(cancellationToken).ConfigureAwait(true);

        CashierProductSearchResultViewModel? exactBarcodeMatch = SearchResults.FirstOrDefault(result =>
            !string.IsNullOrWhiteSpace(result.Barcode) &&
            string.Equals(result.Barcode, normalized, StringComparison.OrdinalIgnoreCase));

        // Enter adds the exact scan match or, failing that, the highlighted suggestion
        CashierProductSearchResultViewModel? productToAdd = exactBarcodeMatch ?? SelectedProduct;
        if (productToAdd is null)
        {
            return;
        }

        AddProductAndResetSearch(productToAdd);
    }

    /// <summary>Adds <paramref name="product"/> to the cart and clears the search box on success (Enter, double click).</summary>
    public bool AddProductAndResetSearch(CashierProductSearchResultViewModel product)
    {
        ArgumentNullException.ThrowIfNull(product);

        SelectedProduct = product;
        if (!TryAddSelectedProduct())
        {
            return false;
        }

        SearchText = string.Empty;
        SearchResults.Clear();
        SelectedProduct = null;
        Message = $"Producto {product.Name} agregado al carrito.";
        return true;
    }

    /// <summary>Moves the highlighted search result up/down; used by arrow keys from the search box.</summary>
    public void MoveSelection(int delta)
    {
        if (SearchResults.Count == 0)
        {
            return;
        }

        int currentIndex = SelectedProduct is null ? -1 : SearchResults.IndexOf(SelectedProduct);
        int nextIndex = Math.Clamp(currentIndex + delta, 0, SearchResults.Count - 1);
        SelectedProduct = SearchResults[nextIndex];
    }

    private void StartAutocompleteSearch()
    {
        CancelAutocompleteSearch();

        string query = SearchText.Trim();
        if (query.Length == 0)
        {
            SearchResults.Clear();
            SelectedProduct = null;
            HasNoMatches = false;
            return;
        }

        _autocompleteSearchCts = new CancellationTokenSource();
        CancellationToken token = _autocompleteSearchCts.Token;
        RunSafe(RunAutocompleteSearchAsync(query, token), ex => Message = $"Error al buscar productos: {ex.Message}");
    }

    private async Task RunAutocompleteSearchAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(AutocompleteDelayMilliseconds, cancellationToken).ConfigureAwait(true);
            await SearchProductsAsync(query, updateMessage: false, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al buscar productos: {ex.Message}";
        }
    }

    private async Task SearchProductsAsync(string query, bool updateMessage, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductLookupResult> results = await _productLookupService.SearchAsync(query, cancellationToken).ConfigureAwait(true);

        if (cancellationToken.IsCancellationRequested || !string.Equals(SearchText.Trim(), query.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SearchResults.Clear();
        foreach (ProductLookupResult result in results)
        {
            SearchResults.Add(new CashierProductSearchResultViewModel(result));
        }

        SelectedProduct = SearchResults.FirstOrDefault();
        HasNoMatches = SearchResults.Count == 0 && query.Trim().Length > 0;

        if (updateMessage)
        {
            Message = SearchResults.Count == 0
                ? "No encontramos productos con ese texto. Revisa el código o el nombre."
                : "Selecciona un producto y presiona Agregar.";
        }
    }

    private void CancelAutocompleteSearch()
    {
        if (_autocompleteSearchCts is null)
        {
            return;
        }

        _autocompleteSearchCts.Cancel();
        _autocompleteSearchCts.Dispose();
        _autocompleteSearchCts = null;
    }

    private bool TryAddSelectedProduct()
    {
        if (SelectedProduct is null)
        {
            Message = "Primero selecciona un producto.";
            return false;
        }

        if (Quantity > SelectedProduct.AvailableStock)
        {
            Message = $"Solo hay {SelectedProduct.AvailableStock:0.##} unidades disponibles de {SelectedProduct.Name}.";
            return false;
        }

        CashierCartItemViewModel? existingItem = CartItems.FirstOrDefault(item => item.ProductId == SelectedProduct.ProductId);
        if (existingItem is not null)
        {
            decimal requestedTotal = existingItem.Quantity + Quantity;
            if (requestedTotal > SelectedProduct.AvailableStock)
            {
                Message = $"No puedes vender {requestedTotal:0.##}; solo hay {SelectedProduct.AvailableStock:0.##} unidades disponibles.";
                return false;
            }

            existingItem.Increase(Quantity);
        }
        else
        {
            CartItems.Add(new CashierCartItemViewModel(SelectedProduct.ProductId, SelectedProduct.Name, SelectedProduct.Barcode, Quantity, SelectedProduct.UnitPrice));
        }

        Quantity = 1m;
        RefreshTotals(updateMessage: false);
        Message = "Producto agregado al carrito.";
        return true;
    }

    private void ClearCart()
    {
        CartItems.Clear();
        ReceivedAmount = 0m;
        DeferredAmount = 0m;
        ClearCustomerAssignment(resetReviewedFlag: true);
        RefreshTotals(updateMessage: false);
        Message = "Carrito limpio. Puedes iniciar una nueva venta.";
    }

    public async Task ConfirmSaleAsync(CancellationToken cancellationToken = default)
    {
        if (!HasCartItems)
        {
            Message = "El carrito está vacío.";
            RefreshPaymentPreview(updateMessage: false);
            return;
        }

        if (!CanConfirm)
        {
            RefreshPaymentPreview();
            return;
        }

        if (ShouldAskForCustomerBeforeInvoice())
        {
            OpenCustomerAssignment(confirmAfterSelection: true);
            return;
        }

        if (_createSaleUseCase is null)
        {
            Message = "Venta validada en prototipo. Falta guardar por Application/SQLite en una parte posterior.";
            return;
        }

        try
        {
            CreateSaleResult result = await _createSaleUseCase.ExecuteAsync(BuildCreateSaleRequest(), cancellationToken).ConfigureAwait(true);
            CartItems.Clear();
            ReceivedAmount = 0m;
            DeferredAmount = 0m;
            ClearCustomerAssignment(resetReviewedFlag: true);
            RefreshTotals(updateMessage: false);
            Message = $"Venta guardada con factura {result.InvoiceNumber}.";

            if (_printReceiptUseCase is not null)
            {
                _pendingPrintSaleId = result.SaleId;
                OnPropertyChanged(nameof(IsAwaitingPrintChoice));
                _printReceiptYesCommand.RaiseCanExecuteChanged();
                _printReceiptNoCommand.RaiseCanExecuteChanged();
            }
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"Error al confirmar la venta: {ex.Message}";
        }
    }

    private void ConfirmPrototypeSale()
    {
        RunSafe(ConfirmSaleAsync(), ex => Message = $"Error al confirmar la venta: {ex.Message}");
    }

    private async Task PrintPendingReceiptAsync()
    {
        if (_pendingPrintSaleId is not { } saleId || _printReceiptUseCase is null)
        {
            return;
        }

        try
        {
            await _printReceiptUseCase.ExecuteAsync(saleId).ConfigureAwait(true);
            Message = "Recibo enviado a la impresora.";
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            Message = $"Error al imprimir el recibo: {ex.Message}";
        }
        finally
        {
            ClearPendingPrintChoice();
        }
    }

    private void DeclinePendingReceipt()
    {
        Message = "Recibo no impreso. Puedes reimprimirlo luego desde Facturas.";
        ClearPendingPrintChoice();
    }

    private void ClearPendingPrintChoice()
    {
        _pendingPrintSaleId = null;
        OnPropertyChanged(nameof(IsAwaitingPrintChoice));
        _printReceiptYesCommand.RaiseCanExecuteChanged();
        _printReceiptNoCommand.RaiseCanExecuteChanged();
    }

    private void EditCartItem(object? parameter)
    {
        if (parameter is not CashierCartItemViewModel item)
        {
            return;
        }

        Message = $"Ajusta la cantidad de {item.ProductName} directamente en la fila.";
    }

    private void RemoveCartItem(object? parameter)
    {
        if (parameter is not CashierCartItemViewModel item)
        {
            return;
        }

        CartItems.Remove(item);
        if (SelectedCartItem == item)
        {
            SelectedCartItem = null;
        }
        RefreshTotals(updateMessage: false);
        Message = $"{item.ProductName} eliminado del carrito.";
    }

    private void AssociateCustomer()
    {
        OpenCustomerAssignment(confirmAfterSelection: false);
    }

    public async Task SearchCustomersAsync(CancellationToken cancellationToken = default)
    {
        if (_searchCustomersUseCase is null)
        {
            Message = "La búsqueda de clientes no está disponible en Caja.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CustomerSearchText))
        {
            Message = "Escribe nombre, cédula, teléfono o correo para buscar el cliente.";
            return;
        }

        IReadOnlyList<CustomerSearchResult> customers = await _searchCustomersUseCase
            .ExecuteAsync(new CustomerSearchRequest(CustomerSearchText, IncludeInactive: false), cancellationToken)
            .ConfigureAwait(true);

        CustomerSearchResults.Clear();
        foreach (CustomerSearchResult customer in customers)
        {
            CustomerSearchResults.Add(customer);
        }

        Message = CustomerSearchResults.Count == 0
            ? "No encontramos clientes activos con ese texto. Puedes facturar sin cliente o registrarlo en Clientes."
            : "Selecciona el cliente para acumular puntos en esta factura.";
    }

    public async Task SelectCustomerAsync(CustomerSearchResult customer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(customer);

        SelectedCustomer = customer;
        CustomerSearchText = customer.FullName;
        CustomerSearchResults.Clear();
        _customerAssignmentReviewed = true;
        IsCustomerAssignmentOpen = false;
        Message = $"Cliente asignado para puntos: {customer.FullName}.";

        await ContinueSaleIfCustomerGateWasOpenAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task ContinueWithoutCustomerAsync(CancellationToken cancellationToken = default)
    {
        ClearCustomerAssignment(resetReviewedFlag: false);
        _customerAssignmentReviewed = true;
        IsCustomerAssignmentOpen = false;
        Message = "Factura sin cliente asignado. No acumulará puntos ni aparecerá en ventas del cliente.";

        await ContinueSaleIfCustomerGateWasOpenAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task SelectCustomerFromCommandAsync(object? parameter)
    {
        if (parameter is CustomerSearchResult customer)
        {
            await SelectCustomerAsync(customer).ConfigureAwait(true);
        }
    }

    private bool ShouldAskForCustomerBeforeInvoice()
        => !_customerAssignmentReviewed && CustomerId is null && DeferredAmount <= 0m;

    private void OpenCustomerAssignment(bool confirmAfterSelection)
    {
        _confirmAfterCustomerAssignment = confirmAfterSelection;
        IsCustomerAssignmentOpen = true;
        Message = confirmAfterSelection
            ? "Antes de facturar, asigna un cliente para puntos o continúa sin cliente."
            : "Busca el cliente para asociarlo a esta venta y al sistema de puntos.";
    }

    private void CloseCustomerAssignment()
    {
        IsCustomerAssignmentOpen = false;
        _confirmAfterCustomerAssignment = false;
        Message = SelectedCustomer is null
            ? "Cliente no asignado todavía."
            : $"Cliente asociado: {SelectedCustomer.FullName}.";
    }

    private async Task ContinueSaleIfCustomerGateWasOpenAsync(CancellationToken cancellationToken)
    {
        if (!_confirmAfterCustomerAssignment)
        {
            return;
        }

        _confirmAfterCustomerAssignment = false;
        await ConfirmSaleAsync(cancellationToken).ConfigureAwait(true);
    }

    private void ClearCustomerAssignment(bool resetReviewedFlag = false)
    {
        SelectedCustomer = null;
        CustomerId = null;
        CustomerSearchText = string.Empty;
        CustomerSearchResults.Clear();
        IsCustomerAssignmentOpen = false;
        if (resetReviewedFlag)
        {
            _customerAssignmentReviewed = false;
            _confirmAfterCustomerAssignment = false;
        }
    }

    private void HoldSale()
    {
        Message = "Venta en espera no disponible todavía. Requiere un caso de uso para guardar y recuperar ventas pendientes.";
    }

    public async Task ApplyRedemptionAsync(CancellationToken cancellationToken = default)
    {
        if (_redeemLoyaltyPointsUseCase is null)
        {
            Message = "El modulo de puntos de lealtad no esta disponible.";
            return;
        }

        if (SelectedCustomer is null)
        {
            Message = "Selecciona un cliente para aplicar descuento por puntos.";
            return;
        }

        if (LoyaltyPointsToRedeem <= 0)
        {
            Message = "Ingresa una cantidad de puntos mayor a cero.";
            return;
        }

        try
        {
            var request = new RedeemPointsRequest(SelectedCustomer.Id, LoyaltyPointsToRedeem, TotalAmount);
            var result = await _redeemLoyaltyPointsUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
            LoyaltyDiscount = result.DiscountAmount;
            Message = $"Descuento de lealtad aplicado: ${result.DiscountAmount:N0} ({result.PointsRedeemed} puntos).";
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
        catch (ArgumentException ex)
        {
            Message = ex.Message;
        }
    }

    private CreateSaleRequest BuildCreateSaleRequest()
    {
        IReadOnlyCollection<CreateSaleItemRequest> items = CartItems
            .Select(item => new CreateSaleItemRequest(item.ProductId, item.Quantity, 0m))
            .ToArray();

        // Use settlement plan when a deferred amount is set; otherwise use legacy full-payment path
        if (DeferredAmount > 0m)
        {
            var plan = new Domain.Entities.SaleSettlementPlan(
                ReceivedAmount,
                SelectedPaymentMethod.Method,
                DeferredAmount);
            return new CreateSaleRequest(CashierUserId, CustomerId, items, plan);
        }

        IReadOnlyCollection<CreateSalePaymentRequest> payments =
        [
            new(
                SelectedPaymentMethod.Method,
                TotalAmount,
                SelectedPaymentMethod.Method == PaymentMethod.Cash ? ReceivedAmount : null,
                string.IsNullOrWhiteSpace(BankOrPlatform) ? null : BankOrPlatform)
        ];

        return new CreateSaleRequest(CashierUserId, CustomerId, items, payments);
    }

    private void RefreshTotals(bool updateMessage = true)
    {
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(CartLineCount));
        OnPropertyChanged(nameof(CartUnitCount));
        OnPropertyChanged(nameof(CartSummaryText));
        OnPropertyChanged(nameof(SubtotalSummaryText));
        OnPropertyChanged(nameof(HasCartItems));
        NotifyPaymentBalanceChanged();
        RefreshPaymentPreview(updateMessage);
        _clearCartCommand.RaiseCanExecuteChanged();
        _setExactAmountCommand.RaiseCanExecuteChanged();
        _holdSaleCommand.RaiseCanExecuteChanged();
    }

    private void RefreshPaymentPreview(bool updateMessage = true)
    {
        // Deferred balance requires an identified customer — block confirmation without one
        if (DeferredAmount > 0m && CustomerId is null)
        {
            ChangeAmount = 0m;
            CanConfirm = false;
            NotifyPaymentBalanceChanged();
            if (updateMessage)
            {
                Message = "Se requiere identificar al cliente para registrar un saldo a crédito.";
            }

            return;
        }

        PaymentPreviewResult preview = _previewPaymentUseCase.Execute(new PaymentPreviewRequest(
            TotalAmount,
            SelectedPaymentMethod.Method,
            ReceivedAmount,
            DeferredAmount));

        ChangeAmount = preview.ChangeAmount;
        CanConfirm = preview.CanConfirm;
        NotifyPaymentBalanceChanged();
        if (updateMessage)
        {
            Message = preview.Message;
        }
    }

    private void OnCartItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (CashierCartItemViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnCartItemPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (CashierCartItemViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnCartItemPropertyChanged;
            }
        }
    }

    private void OnCartItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CashierCartItemViewModel.Quantity) or nameof(CashierCartItemViewModel.Subtotal))
        {
            RefreshTotals(updateMessage: false);
        }
    }

    private void NotifyPaymentBalanceChanged()
    {
        OnPropertyChanged(nameof(IsPaymentShortfall));
        OnPropertyChanged(nameof(PaymentBalanceLabel));
        OnPropertyChanged(nameof(PaymentBalanceText));
    }

    private void UpdateCommandState()
    {
        OnPropertyChanged(nameof(CanAddSelectedProduct));
        _addSelectedProductCommand.RaiseCanExecuteChanged();
    }

    private void AppendReceivedAmount(object? parameter)
    {
        string token = parameter as string ?? string.Empty;

        if (token.Length == 0)
        {
            return;
        }

        if (token == "000")
        {
            ReceivedAmount *= 1000m;
            return;
        }

        if (token == ".")
        {
            return;
        }

        if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int digit))
        {
            return;
        }

        ReceivedAmount = (ReceivedAmount * 10m) + digit;
    }

    private void BackspaceReceivedAmount()
    {
        ReceivedAmount = decimal.Truncate(ReceivedAmount / 10m);
    }

    private void SetExactReceivedAmount()
    {
        ReceivedAmount = TotalAmount;
    }

    private static string FormatCurrency(decimal amount) => $"${amount.ToString("N0", ColombianCulture)}";
}
