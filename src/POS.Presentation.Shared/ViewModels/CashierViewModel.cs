using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Customers;
using POS.Application.DTOs.Loyalty;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Quotes;
using POS.Application.DTOs.Sales;
using POS.Application.Interfaces;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Invoices;
using POS.Application.UseCases.Loyalty;
using POS.Application.UseCases.Products;
using POS.Application.UseCases.Quotes;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Presentation.Shared.Cashier;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class CashierViewModel : ViewModelBase
{
    private const int AutocompleteDelayMilliseconds = 180;
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");
    private readonly IProductLookupService _productLookupService;
    private readonly FindProductByBarcodeUseCase _findProductByBarcodeUseCase;
    private readonly PreviewPaymentUseCase _previewPaymentUseCase;
    private readonly CreateSaleUseCase? _createSaleUseCase;
    private readonly RedeemLoyaltyPointsUseCase? _redeemLoyaltyPointsUseCase;
    private readonly ReprintReceiptUseCase? _printReceiptUseCase;
    private readonly SearchCustomersUseCase? _searchCustomersUseCase;
    private readonly GetCustomerPayableQuotesUseCase? _getCustomerPayableQuotesUseCase;
    private readonly ConvertQuoteToSaleUseCase? _convertQuoteToSaleUseCase;
    private readonly GetLoyaltyBalanceUseCase? _getLoyaltyBalanceUseCase;
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
    private readonly RelayCommand _loadCustomerQuoteCommand;
    private readonly RelayCommand _clearLoadedQuoteCommand;
    private readonly RelayCommand _cancelRedemptionCommand;
    private CancellationTokenSource? _autocompleteSearchCts;
    private readonly SemaphoreSlim _querySemaphore = new(1, 1);
    private int _scannerInputInProgress;
    private string _searchText = string.Empty;
    private string _searchResultsQuery = string.Empty;
    private CashierProductSearchResultViewModel? _selectedProduct;
    private CashierCartItemViewModel? _selectedCartItem;
    private decimal _quantity = 1m;
    private bool _sellSelectedAsBox;
    private PaymentMethodOption _selectedPaymentMethod;
    private decimal _receivedAmount;
    private decimal _deferredAmount;
    private string _message = "Busca o escanea un producto para iniciar la venta.";
    private bool _hasNoMatches;
    private bool _canConfirm;
    private decimal _changeAmount;
    private CustomerSearchResult? _selectedCustomer;
    private CustomerSearchResult? _selectedCustomerSearchResult;
    private Guid? _customerId;
    private string _customerSearchText = string.Empty;
    private decimal _loyaltyDiscount;
    private string _bankOrPlatform = string.Empty;
    private int _loyaltyPointsToRedeem;
    private bool _isCustomerAssignmentOpen;
    private bool _confirmAfterCustomerAssignment;
    private bool _customerAssignmentReviewed;
    private CustomerPayableQuote? _selectedCustomerQuote;
    private CustomerPayableQuote? _loadedQuote;
    private int? _customerLoyaltyBalance;
    private decimal _customerMaxDiscount;
    private decimal _currentTierDiscountPercent;
    private decimal _currentTierPointsMultiplier = 1m;
    private string? _currentTierName;
    private string? _currentTierColorHex;

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
        SearchCustomersUseCase? searchCustomersUseCase = null,
        GetCustomerPayableQuotesUseCase? getCustomerPayableQuotesUseCase = null,
        ConvertQuoteToSaleUseCase? convertQuoteToSaleUseCase = null,
        FindProductByBarcodeUseCase? findProductByBarcodeUseCase = null,
        GetLoyaltyBalanceUseCase? getLoyaltyBalanceUseCase = null)
    {
        _productLookupService = productLookupService ?? throw new ArgumentNullException(nameof(productLookupService));
        _findProductByBarcodeUseCase = findProductByBarcodeUseCase ?? new FindProductByBarcodeUseCase(_productLookupService);
        _previewPaymentUseCase = previewPaymentUseCase ?? throw new ArgumentNullException(nameof(previewPaymentUseCase));
        _createSaleUseCase = createSaleUseCase;
        _redeemLoyaltyPointsUseCase = redeemLoyaltyPointsUseCase;
        _printReceiptUseCase = printReceiptUseCase;
        _searchCustomersUseCase = searchCustomersUseCase;
        _getCustomerPayableQuotesUseCase = getCustomerPayableQuotesUseCase;
        _convertQuoteToSaleUseCase = convertQuoteToSaleUseCase;
        _getLoyaltyBalanceUseCase = getLoyaltyBalanceUseCase;

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
        CustomerQuotes = [];
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
        _loadCustomerQuoteCommand = new RelayCommand(parameter => RunSafe(LoadCustomerQuoteFromCommandAsync(parameter), ex => Message = $"Error al cargar la cotización: {ex.Message}"), _ => SelectedCustomerQuote is not null);
        _clearLoadedQuoteCommand = new RelayCommand(_ => ClearLoadedQuote(), _ => IsQuoteLoaded);
        _printReceiptYesCommand = new RelayCommand(_ => RunSafe(PrintPendingReceiptAsync(), ex => Message = $"Error al imprimir el recibo: {ex.Message}"), _ => IsAwaitingPrintChoice);
        _printReceiptNoCommand = new RelayCommand(_ => DeclinePendingReceipt(), _ => IsAwaitingPrintChoice);
        AddSelectedProductCommand = _addSelectedProductCommand;
        SelectUnitPresentationCommand = new RelayCommand(_ => SellSelectedAsBox = false);
        SelectBoxPresentationCommand = new RelayCommand(_ => SellSelectedAsBox = true, _ => CanSellSelectedAsBox);
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
        _cancelRedemptionCommand = new RelayCommand(_ => CancelRedemption(), _ => LoyaltyDiscount > 0m || LoyaltyPointsToRedeem > 0);
        CancelRedemptionCommand = _cancelRedemptionCommand;
        PrintReceiptYesCommand = _printReceiptYesCommand;
        PrintReceiptNoCommand = _printReceiptNoCommand;
        SearchCustomerCommand = new RelayCommand(_ => RunSafe(SearchCustomersAsync(), ex => Message = $"Error al buscar clientes: {ex.Message}"));
        SelectCustomerCommand = new RelayCommand(parameter => RunSafe(SelectCustomerFromCommandAsync(parameter), ex => Message = $"Error al asignar cliente: {ex.Message}"), parameter => parameter is CustomerSearchResult);
        ConfirmCustomerSelectionCommand = new RelayCommand(_ => RunSafe(ConfirmCustomerSelectionAsync(), ex => Message = $"Error al asignar cliente: {ex.Message}"), _ => SelectedCustomerSearchResult is not null);
        ContinueWithoutCustomerCommand = new RelayCommand(_ => RunSafe(ContinueWithoutCustomerAsync(), ex => Message = $"Error al continuar sin cliente: {ex.Message}"));
        _clearCustomerCommand = new RelayCommand(_ => ClearCustomerAssignment(), _ => CustomerId is not null || SelectedCustomer is not null);
        ClearCustomerCommand = _clearCustomerCommand;
        CloseCustomerAssignmentCommand = new RelayCommand(_ => CloseCustomerAssignment());
        LoadCustomerQuoteCommand = _loadCustomerQuoteCommand;
        ClearLoadedQuoteCommand = _clearLoadedQuoteCommand;
    }

    public event Action? SaleCompleted;

    public ObservableCollection<CashierProductSearchResultViewModel> SearchResults { get; }

    public ObservableCollection<CashierCartItemViewModel> CartItems { get; }

    public ObservableCollection<PaymentMethodOption> PaymentMethods { get; }

    public ObservableCollection<CustomerSearchResult> CustomerSearchResults { get; }

    public ObservableCollection<CustomerPayableQuote> CustomerQuotes { get; }

    public CustomerSearchResult? SelectedCustomerSearchResult
    {
        get => _selectedCustomerSearchResult;
        set
        {
            if (SetProperty(ref _selectedCustomerSearchResult, value))
            {
                (ConfirmCustomerSelectionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand SearchCommand { get; }

    public ICommand AddSelectedProductCommand { get; }

    public ICommand SelectUnitPresentationCommand { get; }

    public ICommand SelectBoxPresentationCommand { get; }

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

    public ICommand ConfirmCustomerSelectionCommand { get; }

    public ICommand ContinueWithoutCustomerCommand { get; }

    public ICommand ClearCustomerCommand { get; }

    public ICommand CloseCustomerAssignmentCommand { get; }

    public ICommand LoadCustomerQuoteCommand { get; }

    public ICommand ClearLoadedQuoteCommand { get; }

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

    /// <summary>Current loyalty point balance for the assigned customer, or null if unavailable.</summary>
    public int? CustomerLoyaltyBalance
    {
        get => _customerLoyaltyBalance;
        private set
        {
            if (SetProperty(ref _customerLoyaltyBalance, value))
            {
                OnPropertyChanged(nameof(CustomerLoyaltyInfo));
                OnPropertyChanged(nameof(HasCustomerLoyaltyInfo));
            }
        }
    }

    /// <summary>Maximum COP discount the assigned customer can get from their points.</summary>
    public decimal CustomerMaxDiscount
    {
        get => _customerMaxDiscount;
        private set
        {
            if (SetProperty(ref _customerMaxDiscount, value))
            {
                OnPropertyChanged(nameof(CustomerLoyaltyInfo));
            }
        }
    }

    /// <summary>True when the assigned customer has loyalty info to display.</summary>
    public bool HasCustomerLoyaltyInfo => _customerLoyaltyBalance.HasValue;

    /// <summary>Human-readable summary of the assigned customer's loyalty points and their money value.</summary>
    public string CustomerLoyaltyInfo
    {
        get
        {
            if (_customerLoyaltyBalance is not { } balance)
            {
                return string.Empty;
            }

            string pointsLabel = balance == 1 ? "1 punto" : $"{balance} puntos";
            return _customerMaxDiscount > 0m
                ? $"{pointsLabel} · equivale a {FormatCurrency(_customerMaxDiscount)} de descuento"
                : $"{pointsLabel} · sin regla de canje activa";
        }
    }

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

    public CustomerPayableQuote? SelectedCustomerQuote
    {
        get => _selectedCustomerQuote;
        set
        {
            if (SetProperty(ref _selectedCustomerQuote, value))
            {
                OnPropertyChanged(nameof(SelectedCustomerQuoteSummary));
                _loadCustomerQuoteCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int CustomerQuoteCount => CustomerQuotes.Count;

    public bool HasCustomerQuotes => CustomerQuotes.Count > 0;

    public bool IsQuoteLoaded => _loadedQuote is not null;

    public string LoadedQuoteNumber => _loadedQuote?.QuoteNumber ?? string.Empty;

    public string LoadedQuoteBanner => _loadedQuote is null
        ? string.Empty
        : $"Cobrando cotización {_loadedQuote.QuoteNumber} · {FormatCurrency(_loadedQuote.Total)}";

    public string SelectedCustomerQuoteSummary => SelectedCustomerQuote is null
        ? string.Empty
        : $"{SelectedCustomerQuote.QuoteNumber} · {FormatCurrency(SelectedCustomerQuote.Total)} · vence {SelectedCustomerQuote.ExpiresAt:dd/MM/yyyy}";

    public decimal LoyaltyDiscount
    {
        get => _loyaltyDiscount;
        private set
        {
            if (SetProperty(ref _loyaltyDiscount, value))
            {
                OnPropertyChanged(nameof(NetPayable));
                OnPropertyChanged(nameof(NetPayableText));
                OnPropertyChanged(nameof(HasLoyaltyDiscount));
                OnPropertyChanged(nameof(LoyaltyDiscountText));
                _cancelRedemptionCommand.RaiseCanExecuteChanged();
                RefreshPaymentPreview(updateMessage: false);
            }
        }
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

    public ICommand CancelRedemptionCommand { get; }

    /// <summary>True when a loyalty discount is currently applied to the sale (drives the discount line and Cancel button).</summary>
    public bool HasLoyaltyDiscount => LoyaltyDiscount > 0m;

    /// <summary>Applied loyalty discount as a negative currency string, e.g. "-$5.000".</summary>
    public string LoyaltyDiscountText => $"-{FormatCurrency(LoyaltyDiscount)}";

    /// <summary>Name of the assigned customer's current loyalty tier, or null when none applies.</summary>
    public string? CurrentTierName => _currentTierName;

    /// <summary>True when the assigned customer currently qualifies for a loyalty tier.</summary>
    public bool HasCurrentTier => _currentTierName is not null;

    /// <summary>Brush for the current tier's color, gray when none/invalid.</summary>
    public System.Windows.Media.Brush CurrentTierColorBrush => TryParseBrush(_currentTierColorHex);

    /// <summary>Automatic discount from the customer's current tier, applied on top of any loyalty point redemption.</summary>
    public decimal TierDiscount => Math.Max(0m, TotalAmount * (_currentTierDiscountPercent / 100m));

    /// <summary>Tier discount as a negative currency string, e.g. "-$5.000".</summary>
    public string TierDiscountText => $"-{FormatCurrency(TierDiscount)}";

    /// <summary>True when the assigned customer's tier grants an automatic discount on this sale.</summary>
    public bool HasTierDiscount => TierDiscount > 0m;

    /// <summary>Human-readable summary of the tier's discount and points multiplier.</summary>
    public string TierBenefitText => _currentTierName is null
        ? string.Empty
        : $"Tier {_currentTierName}: {_currentTierDiscountPercent.ToString("0.##", ColombianCulture)}% dto. · x{_currentTierPointsMultiplier.ToString("0.##", ColombianCulture)} puntos";

    private static System.Windows.Media.Brush TryParseBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return System.Windows.Media.Brushes.Gray;
        }

        try { return (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!; }
        catch { return System.Windows.Media.Brushes.Gray; }
    }

    private void NotifyTierChanged()
    {
        OnPropertyChanged(nameof(CurrentTierName));
        OnPropertyChanged(nameof(HasCurrentTier));
        OnPropertyChanged(nameof(CurrentTierColorBrush));
        OnPropertyChanged(nameof(TierBenefitText));
        OnPropertyChanged(nameof(TierDiscount));
        OnPropertyChanged(nameof(TierDiscountText));
        OnPropertyChanged(nameof(HasTierDiscount));
        OnPropertyChanged(nameof(NetPayable));
        OnPropertyChanged(nameof(NetPayableText));
    }

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
                if (!string.Equals(_searchResultsQuery, _searchText.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    SearchResults.Clear();
                    SelectedProduct = null;
                    HasNoMatches = false;
                }

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
                // Reset the box toggle: it only applies to the product currently selected.
                SellSelectedAsBox = false;
                OnPropertyChanged(nameof(CanSellSelectedAsBox));
                OnPropertyChanged(nameof(SelectedBoxLabel));
                (SelectBoxPresentationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                UpdateCommandState();
            }
        }
    }

    /// <summary>True when the selected product can be sold as a box (has box units + box price).</summary>
    public bool CanSellSelectedAsBox => _selectedProduct?.SupportsBox == true;

    /// <summary>Box description for the selected product, e.g. "Caja x12 · $110.000".</summary>
    public string SelectedBoxLabel => _selectedProduct?.BoxLabel ?? string.Empty;

    /// <summary>When true, the next Add uses the box presentation (price and stock by the box).</summary>
    public bool SellSelectedAsBox
    {
        get => _sellSelectedAsBox;
        set
        {
            bool normalized = value && CanSellSelectedAsBox;
            if (SetProperty(ref _sellSelectedAsBox, normalized))
            {
                OnPropertyChanged(nameof(IsUnitPresentationSelected));
                OnPropertyChanged(nameof(IsBoxPresentationSelected));
            }
        }
    }

    public bool IsUnitPresentationSelected => !SellSelectedAsBox;

    public bool IsBoxPresentationSelected => SellSelectedAsBox;

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

    /// <summary>Amount the customer must actually pay after redeemed loyalty points are applied.</summary>
    public decimal NetPayable => Math.Max(0m, TotalAmount - LoyaltyDiscount - TierDiscount);

    public string NetPayableText => FormatCurrency(NetPayable);

    public int CartLineCount => CartItems.Count;

    public decimal CartUnitCount => CartItems.Sum(item => item.Quantity);

    public string CartSummaryText => $"{CartLineCount} productos · {CartUnitCount.ToString("0.##", ColombianCulture)} unidades";

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
        ReceivedAmount < NetPayable;

    public string PaymentBalanceLabel => IsPaymentShortfall ? "Falta" : "Cambio";

    public string PaymentBalanceText => IsPaymentShortfall ? FormatCurrency(NetPayable - ReceivedAmount) : ChangeText;

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
        if (Interlocked.CompareExchange(ref _scannerInputInProgress, 1, 0) != 0)
        {
            return;
        }

        try
        {
            string normalized = SearchText.Trim();
            if (normalized.Length == 0)
            {
                Message = "Escanea un código o escribe un producto para buscar.";
                return;
            }

            CancelAutocompleteSearch();
            ProductLookupResult? exactMatch;
            await _querySemaphore.WaitAsync(cancellationToken).ConfigureAwait(true);
            try
            {
                exactMatch = await _findProductByBarcodeUseCase
                    .ExecuteAsync(normalized, cancellationToken)
                    .ConfigureAwait(true);
            }
            finally
            {
                _querySemaphore.Release();
            }

            if (exactMatch is null)
            {
                Message = $"No encontramos un producto activo para el código '{normalized}'. Revisa el código e intenta de nuevo.";
                return;
            }

            AddProductAndResetSearch(new CashierProductSearchResultViewModel(exactMatch));
        }
        finally
        {
            Volatile.Write(ref _scannerInputInProgress, 0);
        }
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
        _searchResultsQuery = string.Empty;
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
            _searchResultsQuery = string.Empty;
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
        IReadOnlyList<ProductLookupResult> results;
        await _querySemaphore.WaitAsync(cancellationToken).ConfigureAwait(true);
        try
        {
            results = await _productLookupService.SearchAsync(query, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _querySemaphore.Release();
        }

        if (cancellationToken.IsCancellationRequested || !string.Equals(SearchText.Trim(), query.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Guid? selectedProductId = SelectedProduct?.ProductId;
        SearchResults.Clear();
        foreach (ProductLookupResult result in results)
        {
            SearchResults.Add(new CashierProductSearchResultViewModel(result));
        }

        SelectedProduct = selectedProductId is { } id
            ? SearchResults.FirstOrDefault(result => result.ProductId == id) ?? SearchResults.FirstOrDefault()
            : SearchResults.FirstOrDefault();
        _searchResultsQuery = query.Trim();
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

        bool asBox = SellSelectedAsBox && SelectedProduct.SupportsBox;
        ProductPresentation presentation = asBox ? ProductPresentation.Box : ProductPresentation.Unit;
        int unitsPerLine = asBox ? SelectedProduct.UnitsPerBox!.Value : 1;
        decimal unitPrice = asBox ? SelectedProduct.BoxPrice!.Value : SelectedProduct.UnitPrice;

        // Stock is validated in base units: N boxes consume N * UnitsPerBox units.
        decimal requestedUnits = Quantity * unitsPerLine;
        if (requestedUnits > SelectedProduct.AvailableStock)
        {
            Message = $"Solo hay {SelectedProduct.AvailableStock.ToString("0.##", ColombianCulture)} unidades disponibles de {SelectedProduct.Name}.";
            return false;
        }

        // Only merge with a cart line of the same product AND presentation; a box line and a
        // unit line for the same product are priced differently and must stay separate.
        CashierCartItemViewModel? existingItem = CartItems.FirstOrDefault(item =>
            item.ProductId == SelectedProduct.ProductId && item.Presentation == presentation);
        if (existingItem is not null)
        {
            decimal requestedTotalUnits = (existingItem.Quantity + Quantity) * unitsPerLine;
            if (requestedTotalUnits > SelectedProduct.AvailableStock)
            {
                Message = $"No puedes vender esa cantidad; solo hay {SelectedProduct.AvailableStock.ToString("0.##", ColombianCulture)} unidades disponibles.";
                return false;
            }

            existingItem.Increase(Quantity);
        }
        else
        {
            CartItems.Add(new CashierCartItemViewModel(
                SelectedProduct.ProductId,
                SelectedProduct.Name,
                SelectedProduct.Barcode,
                Quantity,
                unitPrice,
                0m,
                presentation,
                unitsPerLine));
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
        ClearCustomerQuoteCandidates();
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

        if (IsQuoteLoaded)
        {
            await ConfirmLoadedQuoteAsync(cancellationToken).ConfigureAwait(true);
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
            ClearCustomerQuoteCandidates();
            RefreshTotals(updateMessage: false);
            Message = $"Venta guardada con factura {result.InvoiceNumber}.";
            SaleCompleted?.Invoke();

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
            // A confirm-time failure (e.g. the point balance changed since the preview and
            // redemption was rejected) leaves a stale discount driving NetPayable. Clear it so
            // the cart is payable at full price and the cashier can retry or re-apply points.
            ResetRedemption();
            Message = ex.Message;
        }
        catch (Exception ex)
        {
            ResetRedemption();
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

        SelectedCustomerSearchResult = CustomerSearchResults.FirstOrDefault();

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
        SelectedCustomerSearchResult = null;
        _customerAssignmentReviewed = true;
        IsCustomerAssignmentOpen = false;
        await LoadCustomerQuotesForSelectedCustomerAsync(cancellationToken).ConfigureAwait(true);
        await LoadCustomerLoyaltyBalanceAsync(cancellationToken).ConfigureAwait(true);
        Message = HasCustomerQuotes
            ? $"Cliente asignado: {customer.FullName}. Tiene {CustomerQuoteCount} cotización(es) pendiente(s); puedes cargar una en Caja."
            : $"Cliente asignado para puntos: {customer.FullName}.";

        await ContinueSaleIfCustomerGateWasOpenAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task ConfirmCustomerSelectionAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedCustomerSearchResult is null)
        {
            Message = "Selecciona un cliente para asignarlo.";
            return;
        }

        await SelectCustomerAsync(SelectedCustomerSearchResult, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Moves the highlighted customer result for keyboard-only assignment.</summary>
    public void MoveCustomerSelection(int delta)
    {
        if (CustomerSearchResults.Count == 0)
        {
            return;
        }

        int currentIndex = SelectedCustomerSearchResult is null ? -1 : CustomerSearchResults.IndexOf(SelectedCustomerSearchResult);
        int nextIndex = Math.Clamp(currentIndex + delta, 0, CustomerSearchResults.Count - 1);
        SelectedCustomerSearchResult = CustomerSearchResults[nextIndex];
    }

    public async Task ContinueWithoutCustomerAsync(CancellationToken cancellationToken = default)
    {
        ClearCustomerAssignment(resetReviewedFlag: false);
        ClearCustomerQuoteCandidates();
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
        SelectedCustomerSearchResult = null;
        IsCustomerAssignmentOpen = false;
        if (IsQuoteLoaded)
        {
            ClearLoadedQuote(clearCart: true, updateMessage: false);
        }

        ClearCustomerQuoteCandidates();
        ClearCustomerLoyaltyInfo();
        if (resetReviewedFlag)
        {
            _customerAssignmentReviewed = false;
            _confirmAfterCustomerAssignment = false;
        }
    }

    private async Task LoadCustomerQuotesForSelectedCustomerAsync(CancellationToken cancellationToken)
    {
        ClearCustomerQuoteCandidates();

        if (_getCustomerPayableQuotesUseCase is null || CustomerId is not { } customerId)
        {
            return;
        }

        IReadOnlyList<CustomerPayableQuote> quotes = await _getCustomerPayableQuotesUseCase
            .ExecuteAsync(customerId, cancellationToken)
            .ConfigureAwait(true);

        foreach (CustomerPayableQuote quote in quotes)
        {
            CustomerQuotes.Add(quote);
        }

        SelectedCustomerQuote = CustomerQuotes.FirstOrDefault();
        NotifyCustomerQuotesChanged();
    }

    private async Task LoadCustomerLoyaltyBalanceAsync(CancellationToken cancellationToken)
    {
        ClearCustomerLoyaltyInfo();

        if (_getLoyaltyBalanceUseCase is null || CustomerId is not { } customerId)
        {
            return;
        }

        try
        {
            LoyaltyBalanceResult? balance = await _getLoyaltyBalanceUseCase
                .ExecuteAsync(customerId, recentTransactionCount: 0, cancellationToken)
                .ConfigureAwait(true);

            if (balance is null)
            {
                return;
            }

            CustomerLoyaltyBalance = balance.Balance;
            CustomerMaxDiscount = balance.RedemptionValuePerPoint > 0m
                ? balance.Balance * balance.RedemptionValuePerPoint
                : 0m;

            _currentTierDiscountPercent = balance.CurrentTier?.DiscountPercent ?? 0m;
            _currentTierPointsMultiplier = balance.CurrentTier?.PointsMultiplier ?? 1m;
            _currentTierName = balance.CurrentTier?.Name;
            _currentTierColorHex = balance.CurrentTier?.Color;
            NotifyTierChanged();
        }
        catch (Exception)
        {
            // Loyalty balance is supplementary info — never block customer assignment on failure.
        }
    }

    private void ClearCustomerLoyaltyInfo()
    {
        CustomerLoyaltyBalance = null;
        CustomerMaxDiscount = 0m;
        _currentTierDiscountPercent = 0m;
        _currentTierPointsMultiplier = 1m;
        _currentTierName = null;
        _currentTierColorHex = null;
        NotifyTierChanged();
    }

    private async Task LoadCustomerQuoteFromCommandAsync(object? parameter)
    {
        CustomerPayableQuote? quote = parameter as CustomerPayableQuote ?? SelectedCustomerQuote;
        await LoadCustomerQuoteAsync(quote).ConfigureAwait(true);
    }

    public Task LoadCustomerQuoteAsync(CustomerPayableQuote? quote, CancellationToken cancellationToken = default)
    {
        if (quote is null)
        {
            Message = "Selecciona una cotización pendiente para cargarla en Caja.";
            return Task.CompletedTask;
        }

        if (CustomerId is null)
        {
            Message = "Asigna el cliente antes de cargar una cotización.";
            return Task.CompletedTask;
        }

        CartItems.Clear();
        foreach (CustomerPayableQuoteItem item in quote.Items)
        {
            CartItems.Add(new CashierCartItemViewModel(
                item.ProductId,
                item.ProductName,
                CompactProductId(item.ProductId),
                item.Quantity,
                item.UnitPrice,
                item.Discount));
        }

        _loadedQuote = quote;
        SelectedCustomerQuote = quote;
        _customerAssignmentReviewed = true;
        DeferredAmount = 0m;
        if (SelectedPaymentMethod.Method == PaymentMethod.Cash)
        {
            ReceivedAmount = quote.Total;
        }

        NotifyQuoteContextChanged();
        RefreshTotals(updateMessage: false);
        Message = $"Cotización {quote.QuoteNumber} cargada en Caja. Revisa el método de pago y finaliza la venta.";
        return Task.CompletedTask;
    }

    private void ClearLoadedQuote(bool clearCart = true, bool updateMessage = true)
    {
        if (clearCart)
        {
            CartItems.Clear();
        }

        _loadedQuote = null;
        NotifyQuoteContextChanged();
        RefreshTotals(updateMessage: false);
        if (updateMessage)
        {
            Message = "Cotización retirada de Caja. Puedes iniciar una venta normal.";
        }
    }

    private void ClearCustomerQuoteCandidates()
    {
        CustomerQuotes.Clear();
        SelectedCustomerQuote = null;
        NotifyCustomerQuotesChanged();
    }

    private void NotifyCustomerQuotesChanged()
    {
        OnPropertyChanged(nameof(CustomerQuoteCount));
        OnPropertyChanged(nameof(HasCustomerQuotes));
    }

    private void NotifyQuoteContextChanged()
    {
        OnPropertyChanged(nameof(IsQuoteLoaded));
        OnPropertyChanged(nameof(LoadedQuoteNumber));
        OnPropertyChanged(nameof(LoadedQuoteBanner));
        _clearLoadedQuoteCommand.RaiseCanExecuteChanged();
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

        // The discount must apply to a real sale. With an empty cart TotalAmount is 0, and the
        // use case skips its "cap to sale total" guard when SaleTotal is 0 (that path is meant for
        // standalone loyalty redemptions), so it would report a phantom discount that nets to 0 —
        // and any later cart change wipes the preview via ResetRedemption. Block it up front.
        if (TotalAmount <= 0m)
        {
            Message = "Agrega productos al carrito antes de canjear puntos.";
            return;
        }

        if (LoyaltyPointsToRedeem <= 0)
        {
            Message = "Ingresa una cantidad de puntos mayor a cero.";
            return;
        }

        if (DeferredAmount > 0m)
        {
            Message = "No se puede combinar canje de puntos con saldo a crédito. Cancela el crédito para aplicar puntos.";
            return;
        }

        try
        {
            // Preview only — points are NOT spent here. The actual redemption is recorded
            // atomically when the sale is confirmed (CreateSaleUseCase), so cancelling the
            // sale never burns points.
            var request = new RedeemPointsRequest(SelectedCustomer.Id, LoyaltyPointsToRedeem, TotalAmount);
            var result = await _redeemLoyaltyPointsUseCase.PreviewAsync(request, cancellationToken).ConfigureAwait(true);
            LoyaltyPointsToRedeem = result.PointsRedeemed;
            LoyaltyDiscount = result.DiscountAmount;
            Message = $"Descuento de lealtad aplicado: ${result.DiscountAmount.ToString("N0", ColombianCulture)} ({result.PointsRedeemed} puntos). Se descontarán al confirmar la venta.";
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
        // CreateSaleItemRequest.Discount is absolute money (see ConvertQuoteToSaleUseCase, which
        // converts the same way) — CashierCartItemViewModel.Discount is a UI-facing percentage, so
        // it must be converted here, not passed through raw.
        IReadOnlyCollection<CreateSaleItemRequest> items = CartItems
            .Select(item =>
            {
                decimal effectiveDiscountPercent = CompoundDiscount(item.Discount, _currentTierDiscountPercent);
                decimal discountMoney = decimal.Round(item.UnitPrice * item.Quantity * effectiveDiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
                return new CreateSaleItemRequest(item.ProductId, item.Quantity, discountMoney, item.Presentation);
            })
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

        // The customer only pays the net (total minus redeemed points); the loyalty tender
        // covering the discount is added server-side by CreateSaleUseCase.
        IReadOnlyCollection<CreateSalePaymentRequest> payments =
        [
            new(
                SelectedPaymentMethod.Method,
                NetPayable,
                SelectedPaymentMethod.Method == PaymentMethod.Cash ? ReceivedAmount : null,
                string.IsNullOrWhiteSpace(BankOrPlatform) ? null : BankOrPlatform)
        ];

        int redeemPoints = LoyaltyDiscount > 0m ? LoyaltyPointsToRedeem : 0;
        return new CreateSaleRequest(CashierUserId, CustomerId, items, payments, redeemPoints);
    }

    /// <summary>
    /// Compounds an automatic tier discount percentage on top of a line's existing (possibly manual)
    /// discount percentage, so the final line total is exactly (existing-discounted subtotal) reduced
    /// by tierPercent — matching the aggregate TierDiscount shown at checkout with no rounding drift.
    /// </summary>
    private static decimal CompoundDiscount(decimal existingDiscountPercent, decimal tierDiscountPercent)
    {
        if (tierDiscountPercent <= 0m)
        {
            return existingDiscountPercent;
        }

        decimal remainingFactor = (1m - existingDiscountPercent / 100m) * (1m - tierDiscountPercent / 100m);
        return decimal.Round(100m - (remainingFactor * 100m), 4, MidpointRounding.AwayFromZero);
    }

    private async Task ConfirmLoadedQuoteAsync(CancellationToken cancellationToken)
    {
        if (_loadedQuote is null)
        {
            return;
        }

        if (_convertQuoteToSaleUseCase is null)
        {
            Message = "La conversión de cotizaciones no está disponible en Caja.";
            return;
        }

        if (CashierUserId == Guid.Empty)
        {
            Message = "No hay cajero autenticado para facturar la cotización.";
            return;
        }

        try
        {
            CustomerPayableQuote quote = _loadedQuote;
            var request = new ConvertQuoteToSaleRequest(
                quote.QuoteId,
                CashierUserId,
                BuildPaymentRequests());

            CreateSaleResult result = DeferredAmount > 0m
                ? await _convertQuoteToSaleUseCase.ExecuteAsync(request, BuildSettlementPlan(), cancellationToken).ConfigureAwait(true)
                : await _convertQuoteToSaleUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

            CartItems.Clear();
            ReceivedAmount = 0m;
            DeferredAmount = 0m;
            ClearCustomerAssignment(resetReviewedFlag: true);
            RefreshTotals(updateMessage: false);
            Message = $"Cotización {quote.QuoteNumber} facturada con factura {result.InvoiceNumber}.";
            SaleCompleted?.Invoke();

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
            Message = $"Error al facturar la cotización: {ex.Message}";
        }
    }

    private IReadOnlyCollection<CreateSalePaymentRequest> BuildPaymentRequests()
    {
        return
        [
            new(
                SelectedPaymentMethod.Method,
                TotalAmount,
                SelectedPaymentMethod.Method == PaymentMethod.Cash ? ReceivedAmount : null,
                string.IsNullOrWhiteSpace(BankOrPlatform) ? null : BankOrPlatform)
        ];
    }

    private SaleSettlementPlan BuildSettlementPlan()
        => new(ReceivedAmount, SelectedPaymentMethod.Method, DeferredAmount);

    private void RefreshTotals(bool updateMessage = true)
    {
        OnPropertyChanged(nameof(TotalAmount));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(NetPayable));
        OnPropertyChanged(nameof(NetPayableText));
        OnPropertyChanged(nameof(TierDiscount));
        OnPropertyChanged(nameof(TierDiscountText));
        OnPropertyChanged(nameof(HasTierDiscount));
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
            NetPayable,
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

        // Any change to the cart (add/remove/clear) invalidates a previewed redemption.
        ResetRedemption();
    }

    private void OnCartItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CashierCartItemViewModel.Quantity) or nameof(CashierCartItemViewModel.Discount) or nameof(CashierCartItemViewModel.Subtotal))
        {
            ResetRedemption();
            RefreshTotals(updateMessage: false);
        }
    }

    /// <summary>
    /// Clears any previewed loyalty redemption so a discount computed against an old cart
    /// total can never be carried into a different sale. Points are only ever spent when the
    /// sale is confirmed, so this simply discards the preview.
    /// </summary>
    private void ResetRedemption()
    {
        if (_loyaltyDiscount == 0m && _loyaltyPointsToRedeem == 0)
        {
            return;
        }

        LoyaltyDiscount = 0m;
        LoyaltyPointsToRedeem = 0;
    }

    /// <summary>
    /// Cashier-triggered cancel: discards the previewed redemption so the customer keeps their
    /// points and pays full price. Points are only spent at sale confirmation, so nothing is lost.
    /// </summary>
    private void CancelRedemption()
    {
        bool hadDiscount = _loyaltyDiscount > 0m || _loyaltyPointsToRedeem > 0;
        ResetRedemption();
        Message = hadDiscount
            ? "Canje cancelado. El cliente conserva sus puntos y paga el total completo."
            : "No hay ningún canje aplicado para cancelar.";
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
        ReceivedAmount = NetPayable;
    }

    private static string FormatCurrency(decimal amount) => $"${amount.ToString("N0", ColombianCulture)}";

    private static string CompactProductId(Guid productId)
        => productId.ToString("N")[..8].ToUpperInvariant();
}
