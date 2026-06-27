using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Customers;
using POS.Application.DTOs.Loyalty;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Sales;
using POS.Application.Interfaces;
using POS.Application.UseCases.Loyalty;
using POS.Application.UseCases.Sales;
using POS.Domain.Enums;
using POS.Presentation.Shared.Cashier;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class CashierViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");
    private readonly IProductLookupService _productLookupService;
    private readonly PreviewPaymentUseCase _previewPaymentUseCase;
    private readonly CreateSaleUseCase? _createSaleUseCase;
    private readonly RedeemLoyaltyPointsUseCase? _redeemLoyaltyPointsUseCase;
    private readonly RelayCommand _addSelectedProductCommand;
    private readonly RelayCommand _clearCartCommand;
    private readonly RelayCommand _confirmPrototypeSaleCommand;
    private string _searchText = string.Empty;
    private ProductLookupResult? _selectedProduct;
    private decimal _quantity = 1m;
    private PaymentMethodOption _selectedPaymentMethod;
    private decimal _receivedAmount;
    private decimal _deferredAmount;
    private string _message = "Busca o escanea un producto para iniciar la venta.";
    private bool _canConfirm;
    private decimal _changeAmount;
    private CustomerResult? _selectedCustomer;
    private decimal _loyaltyDiscount;
    private string _bankOrPlatform = string.Empty;
    private int _loyaltyPointsToRedeem;

    public CashierViewModel(IProductLookupService productLookupService)
        : this(productLookupService, new PreviewPaymentUseCase())
    {
    }

    public CashierViewModel(IProductLookupService productLookupService, PreviewPaymentUseCase previewPaymentUseCase)
        : this(productLookupService, previewPaymentUseCase, null, null)
    {
    }

    public CashierViewModel(IProductLookupService productLookupService, CreateSaleUseCase createSaleUseCase)
        : this(productLookupService, new PreviewPaymentUseCase(), createSaleUseCase, null)
    {
    }

    public CashierViewModel(
        IProductLookupService productLookupService,
        PreviewPaymentUseCase previewPaymentUseCase,
        CreateSaleUseCase? createSaleUseCase)
        : this(productLookupService, previewPaymentUseCase, createSaleUseCase, null)
    {
    }

    public CashierViewModel(
        IProductLookupService productLookupService,
        PreviewPaymentUseCase previewPaymentUseCase,
        CreateSaleUseCase? createSaleUseCase,
        RedeemLoyaltyPointsUseCase? redeemLoyaltyPointsUseCase)
    {
        _productLookupService = productLookupService ?? throw new ArgumentNullException(nameof(productLookupService));
        _previewPaymentUseCase = previewPaymentUseCase ?? throw new ArgumentNullException(nameof(previewPaymentUseCase));
        _createSaleUseCase = createSaleUseCase;
        _redeemLoyaltyPointsUseCase = redeemLoyaltyPointsUseCase;

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

        SearchCommand = new RelayCommand(async _ => await SearchAsync().ConfigureAwait(true));
        _addSelectedProductCommand = new RelayCommand(_ => AddSelectedProduct(), _ => CanAddSelectedProduct);
        _clearCartCommand = new RelayCommand(_ => ClearCart(), _ => CartItems.Count > 0);
        _confirmPrototypeSaleCommand = new RelayCommand(_ => ConfirmPrototypeSale(), _ => CanConfirm);
        AddSelectedProductCommand = _addSelectedProductCommand;
        ClearCartCommand = _clearCartCommand;
        ConfirmPrototypeSaleCommand = _confirmPrototypeSaleCommand;
        ApplyRedemptionCommand = new RelayCommand(async _ => await ApplyRedemptionAsync().ConfigureAwait(true));
    }

    public ObservableCollection<ProductLookupResult> SearchResults { get; }

    public ObservableCollection<CashierCartItemViewModel> CartItems { get; }

    public ObservableCollection<PaymentMethodOption> PaymentMethods { get; }

    public ICommand SearchCommand { get; }

    public ICommand AddSelectedProductCommand { get; }

    public ICommand ClearCartCommand { get; }

    public ICommand ConfirmPrototypeSaleCommand { get; }

    public Guid CashierUserId { get; set; }

    public Guid? CustomerId { get; set; }

    public CustomerResult? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                CustomerId = value?.Id;
            }
        }
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

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value ?? string.Empty);
    }

    public ProductLookupResult? SelectedProduct
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

    public decimal TotalAmount => CartItems.Sum(item => item.Subtotal);

    public string TotalText => FormatCurrency(TotalAmount);

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

    public async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ProductLookupResult> results = await _productLookupService.SearchAsync(SearchText, cancellationToken).ConfigureAwait(true);

        SearchResults.Clear();
        foreach (ProductLookupResult result in results)
        {
            SearchResults.Add(result);
        }

        SelectedProduct = SearchResults.FirstOrDefault();
        Message = SearchResults.Count == 0
            ? "No encontramos productos con ese texto. Revisa el código o el nombre."
            : "Selecciona un producto y presiona Agregar.";
    }

    private void AddSelectedProduct()
    {
        if (SelectedProduct is null)
        {
            Message = "Primero selecciona un producto.";
            return;
        }

        if (Quantity > SelectedProduct.AvailableStock)
        {
            Message = $"Solo hay {SelectedProduct.AvailableStock:0.##} unidades disponibles de {SelectedProduct.Name}.";
            return;
        }

        CashierCartItemViewModel? existingItem = CartItems.FirstOrDefault(item => item.ProductId == SelectedProduct.ProductId);
        if (existingItem is not null)
        {
            decimal requestedTotal = existingItem.Quantity + Quantity;
            if (requestedTotal > SelectedProduct.AvailableStock)
            {
                Message = $"No puedes vender {requestedTotal:0.##}; solo hay {SelectedProduct.AvailableStock:0.##} unidades disponibles.";
                return;
            }

            existingItem.Increase(Quantity);
        }
        else
        {
            CartItems.Add(new CashierCartItemViewModel(SelectedProduct.ProductId, SelectedProduct.Name, Quantity, SelectedProduct.UnitPrice));
        }

        Quantity = 1m;
        RefreshTotals(updateMessage: false);
        Message = "Producto agregado al carrito.";
    }

    private void ClearCart()
    {
        CartItems.Clear();
        ReceivedAmount = 0m;
        DeferredAmount = 0m;
        RefreshTotals(updateMessage: false);
        Message = "Carrito limpio. Puedes iniciar una nueva venta.";
    }

    public async Task ConfirmSaleAsync(CancellationToken cancellationToken = default)
    {
        if (!CanConfirm)
        {
            RefreshPaymentPreview();
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
            RefreshTotals(updateMessage: false);
            Message = $"Venta guardada con factura {result.InvoiceNumber}.";
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }

    private void ConfirmPrototypeSale()
    {
        _ = ConfirmSaleAsync();
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
            var request = new RedeemPointsRequest(SelectedCustomer.Id, LoyaltyPointsToRedeem);
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
        RefreshPaymentPreview(updateMessage);
        _clearCartCommand.RaiseCanExecuteChanged();
    }

    private void RefreshPaymentPreview(bool updateMessage = true)
    {
        // Deferred balance requires an identified customer — block confirmation without one
        if (DeferredAmount > 0m && CustomerId is null)
        {
            ChangeAmount = 0m;
            CanConfirm = false;
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
        if (updateMessage)
        {
            Message = preview.Message;
        }
    }

    private void UpdateCommandState()
    {
        OnPropertyChanged(nameof(CanAddSelectedProduct));
        _addSelectedProductCommand.RaiseCanExecuteChanged();
    }

    private static string FormatCurrency(decimal amount) => $"${amount.ToString("N0", ColombianCulture)}";
}
