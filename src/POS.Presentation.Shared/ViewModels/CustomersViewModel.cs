using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Customers;
using POS.Application.DTOs.Loyalty;
using POS.Application.DTOs.Receivables;
using POS.Application.Interfaces;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Loyalty;
using POS.Application.Common;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.Commands;
using POS.Presentation.Shared.Cashier;

namespace POS.Presentation.Shared.ViewModels;

public sealed class CustomersViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    private readonly SaveCustomerUseCase _saveCustomer;
    private readonly GetCustomerHistoryUseCase _getHistory;
    private readonly SearchCustomersUseCase _searchCustomers;
    private readonly GetLoyaltyBalanceUseCase? _getLoyaltyBalance;
    private readonly IReceivableQueryService? _receivableQuery;
    private readonly IApplyReceivablePaymentUseCase? _applyPayment;
    private readonly ICurrentOperatorSession? _session;
    private readonly ICashierShiftRepository? _cashierShifts;
    private bool _isReloading;
    private CancellationTokenSource? _customerLoadingCts;
    private CustomerSearchResult? _selectedCustomer;
    private ReceivableSummary? _selectedReceivable;
    private decimal _abonoAmount;
    private string _abonoAmountText = string.Empty;
    private PaymentMethodOption _selectedSettlementPaymentMethod;
    private string _selectedSettlementBankOrPlatform = string.Empty;
    private string _searchText = string.Empty;
    private string _fullName = string.Empty;
    private string _identification = string.Empty;
    private string? _phone;
    private string? _email;
    private string? _address;
    private bool _isActive = true;
    private bool _includeInactive;
    private string _message = "Selecciona un cliente o registra uno nuevo.";
    private int _loyaltyPoints;
    private string _currentTierName = string.Empty;
    private string _currentTierBenefit = string.Empty;
    private string? _currentTierColorHex;
    private POS.Domain.Enums.LoyaltyTierTextForeground _currentTierTextForeground = POS.Domain.Enums.LoyaltyTierTextForeground.White;
    private int _totalCustomersCount;
    private int _activeCustomersCount;
    private string _historyFilter = "all";
    private decimal _outstandingBalance;

    public CustomersViewModel(
        SaveCustomerUseCase saveCustomer,
        GetCustomerHistoryUseCase getHistory,
        SearchCustomersUseCase searchCustomers,
        GetLoyaltyBalanceUseCase? getLoyaltyBalance = null,
        IReceivableQueryService? receivableQuery = null,
        IApplyReceivablePaymentUseCase? applyPayment = null,
        ICurrentOperatorSession? session = null,
        ICashierShiftRepository? cashierShifts = null)
    {
        _saveCustomer = saveCustomer;
        _getHistory = getHistory;
        _searchCustomers = searchCustomers;
        _getLoyaltyBalance = getLoyaltyBalance;
        _receivableQuery = receivableQuery;
        _applyPayment = applyPayment;
        _session = session;
        _cashierShifts = cashierShifts;

        SettlementPaymentMethods =
        [
            new(PaymentMethod.Cash, "Efectivo"),
            new(PaymentMethod.DebitCard, "Débito"),
            new(PaymentMethod.CreditCard, "Tarjeta de crédito"),
            new(PaymentMethod.Transfer, "Transferencia")
        ];
        _selectedSettlementPaymentMethod = SettlementPaymentMethods[0];

        Customers.CollectionChanged += OnCustomersCollectionChanged;
        HistoryEntries.CollectionChanged += OnHistoryEntriesCollectionChanged;

        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar clientes: {ex.Message}"));
        AbonarCommand = new RelayCommand(_ => RunSafe(AbonarAsync(), ex => Message = $"Error al registrar abono: {ex.Message}"), _ => CanAbonar);
        PagarTodoSaldoCommand = new RelayCommand(_ => RunSafe(PagarTodoSaldoAsync(), ex => Message = $"Error al pagar el saldo total: {ex.Message}"), _ => CanPagarTodoSaldo);
        SaveCommand = new RelayCommand(_ => RunSafe(SaveAsync(), ex => Message = $"Error al guardar cliente: {ex.Message}"));
        DeleteCommand = new RelayCommand(_ => RunSafe(DeleteAsync(), ex => Message = $"Error al eliminar cliente: {ex.Message}"), _ => SelectedCustomer is not null);
        NewCommand = new RelayCommand(_ => ClearForm());
        OpenHistoryCommand = new RelayCommand(_ => RunSafe(OpenHistoryAsync(), ex => Message = $"Error al cargar historial: {ex.Message}"), _ => SelectedCustomer is not null);
        OpenInvoiceCommand = new RelayCommand(OpenInvoiceFromHistory, CanOpenInvoiceFromHistory);
        SetHistoryFilterCommand = new RelayCommand(filter => HistoryFilter = filter as string ?? "all");
    }

    public event Action<Guid>? InvoiceOpenRequested;

    public ObservableCollection<CustomerSearchResult> Customers { get; } = [];

    /// <summary>Full, unfiltered history for the selected customer. Kept for backward compatibility with existing consumers/tests.</summary>
    public ObservableCollection<CustomerHistoryEntry> HistoryEntries { get; } = [];

    /// <summary>History entries restricted by <see cref="HistoryFilter"/> ("all" / "sale" / "quote") for display.</summary>
    public ObservableCollection<CustomerHistoryEntry> FilteredHistoryEntries { get; } = [];

    /// <summary>Open (unpaid) receivables for the selected customer — the user picks one to register a payment against.</summary>
    public ObservableCollection<ReceivableSummary> OpenReceivables { get; } = [];

    public ObservableCollection<PaymentMethodOption> SettlementPaymentMethods { get; }

    public IReadOnlyList<string> SettlementPaymentPlatforms { get; } = PaymentPlatforms.All;

    public PaymentMethodOption SelectedSettlementPaymentMethod
    {
        get => _selectedSettlementPaymentMethod;
        set
        {
            if (value is null || !SetProperty(ref _selectedSettlementPaymentMethod, value))
            {
                return;
            }

            if (value.Method != PaymentMethod.Transfer)
            {
                SelectedSettlementBankOrPlatform = string.Empty;
            }

            OnPropertyChanged(nameof(IsSettlementTransfer));
            RaiseAbonarCanExecuteChanged();
        }
    }

    public string SelectedSettlementBankOrPlatform
    {
        get => _selectedSettlementBankOrPlatform;
        set
        {
            if (SetProperty(ref _selectedSettlementBankOrPlatform, value))
            {
                RaiseAbonarCanExecuteChanged();
            }
        }
    }

    public bool IsSettlementTransfer => SelectedSettlementPaymentMethod.Method == PaymentMethod.Transfer;

    public ICommand LoadCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand NewCommand { get; }

    public ICommand OpenHistoryCommand { get; }

    public ICommand OpenInvoiceCommand { get; }

    public ICommand SetHistoryFilterCommand { get; }

    public ICommand AbonarCommand { get; }

    public ICommand PagarTodoSaldoCommand { get; }

    /// <summary>Receivable the payment will be applied to. Defaults to the newest open one when history loads.</summary>
    public ReceivableSummary? SelectedReceivable
    {
        get => _selectedReceivable;
        set
        {
            if (SetProperty(ref _selectedReceivable, value))
            {
                RaiseAbonarCanExecuteChanged();
            }
        }
    }

    /// <summary>Amount to abonar against <see cref="SelectedReceivable"/>. The use case rejects zero/overpayment.</summary>
    public decimal AbonoAmount
    {
        get => _abonoAmount;
        set
        {
            if (SetProperty(ref _abonoAmount, value))
            {
                _abonoAmountText = value == 0m
                    ? string.Empty
                    : value.ToString("N0", ColombianCulture);
                OnPropertyChanged(nameof(AbonoAmountText));
                RaiseAbonarCanExecuteChanged();
            }
        }
    }

    public string AbonoAmountText
    {
        get => _abonoAmountText;
        set
        {
            if (!SetProperty(ref _abonoAmountText, value))
            {
                return;
            }

            decimal parsedAmount = TryParseAbonoAmount(value, out decimal amount) ? amount : 0m;
            SetProperty(ref _abonoAmount, parsedAmount, nameof(AbonoAmount));

            RaiseAbonarCanExecuteChanged();
        }
    }

    /// <summary>True when payments can be registered — needs the use case wired, a target receivable, and a positive amount.</summary>
    public bool CanAbonar =>
        _applyPayment is not null &&
        SelectedReceivable is not null &&
        AbonoAmount > 0m &&
        (!IsSettlementTransfer || PaymentPlatforms.IsValid(SelectedSettlementBankOrPlatform));

    public bool CanPagarTodoSaldo =>
        _applyPayment is not null &&
        SelectedCustomer is not null &&
        OutstandingBalance > 0m &&
        (!IsSettlementTransfer || PaymentPlatforms.IsValid(SelectedSettlementBankOrPlatform));

    public CustomerSearchResult? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (_isReloading && value is null)
            {
                return; // Ignore selection clearing during collection reload
            }

            if (SetProperty(ref _selectedCustomer, value))
            {
                // Cancel ongoing loyalty/history loads
                _customerLoadingCts?.Cancel();
                _customerLoadingCts?.Dispose();
                _customerLoadingCts = new CancellationTokenSource();
                var token = _customerLoadingCts.Token;

                if (value is not null)
                {
                    FullName = value.FullName;
                    Identification = value.Identification;
                    Phone = value.Phone;
                    Email = value.Email;
                    Address = value.Address;
                    IsActive = value.IsActive;

                    // OpenHistoryAsync will internally load loyalty points too, so we only trigger OpenHistoryAsync
                    RunSafe(OpenHistoryAsync(token), ex => Message = $"Error al cargar historial: {ex.Message}");
                }
                else
                {
                    LoyaltyPoints = 0;
                    ApplyCurrentTier(null);
                    OutstandingBalance = 0m;
                    HistoryEntries.Clear();
                    OpenReceivables.Clear();
                    SelectedReceivable = null;
                    AbonoAmount = 0m;
                }

                if (OpenHistoryCommand is RelayCommand openHistoryCommand)
                {
                    openHistoryCommand.RaiseCanExecuteChanged();
                }

                if (DeleteCommand is RelayCommand deleteCommand)
                {
                    deleteCommand.RaiseCanExecuteChanged();
                }

                OnPropertyChanged(nameof(HasSelectedCustomer));
                RaiseAbonarCanExecuteChanged();
            }
        }
    }

    /// <summary>True when a customer is selected — drives form title, status chip, and delete-button visibility.</summary>
    public bool HasSelectedCustomer => SelectedCustomer is not null;

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    public string Identification
    {
        get => _identification;
        set => SetProperty(ref _identification, value);
    }

    public string? Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }

    public string? Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string? Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IncludeInactive
    {
        get => _includeInactive;
        set => SetProperty(ref _includeInactive, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    /// <summary>Loyalty points available for the selected customer (0 when none/no customer selected).</summary>
    public int LoyaltyPoints
    {
        get => _loyaltyPoints;
        private set => SetProperty(ref _loyaltyPoints, value);
    }

    /// <summary>Name of the selected customer's current loyalty tier, empty when none applies.</summary>
    public string CurrentTierName
    {
        get => _currentTierName;
        private set => SetProperty(ref _currentTierName, value);
    }

    /// <summary>Formatted benefit summary for the current tier, e.g. "10% descuento · x2 puntos".</summary>
    public string CurrentTierBenefit
    {
        get => _currentTierBenefit;
        private set => SetProperty(ref _currentTierBenefit, value);
    }

    /// <summary>True when the selected customer currently qualifies for a loyalty tier.</summary>
    public bool HasCurrentTier => !string.IsNullOrEmpty(_currentTierName);

    /// <summary>Brush for the current tier's color, gray when none/invalid.</summary>
    public System.Windows.Media.Brush CurrentTierColorBrush => TryParseBrush(_currentTierColorHex);
    public System.Windows.Media.Brush CurrentTierTextForegroundBrush => _currentTierTextForeground == POS.Domain.Enums.LoyaltyTierTextForeground.Black
        ? System.Windows.Media.Brushes.Black
        : System.Windows.Media.Brushes.White;

    private static System.Windows.Media.Brush TryParseBrush(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return System.Windows.Media.Brushes.Gray;
        }

        try { return (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!; }
        catch { return System.Windows.Media.Brushes.Gray; }
    }

    private void ApplyCurrentTier(LoyaltyTierResult? tier)
    {
        CurrentTierName = tier?.Name ?? string.Empty;
        CurrentTierBenefit = tier is null
            ? string.Empty
            : $"{tier.DiscountPercent.ToString("0.##", ColombianCulture)}% descuento · x{tier.PointsMultiplier.ToString("0.##", ColombianCulture)} puntos";
        _currentTierColorHex = tier?.Color;
        _currentTierTextForeground = tier?.TextForeground ?? POS.Domain.Enums.LoyaltyTierTextForeground.White;
        OnPropertyChanged(nameof(HasCurrentTier));
        OnPropertyChanged(nameof(CurrentTierColorBrush));
        OnPropertyChanged(nameof(CurrentTierTextForegroundBrush));
    }

    /// <summary>Count of currently loaded customers (reflects the active search/filter, not a separate global aggregate).</summary>
    public int TotalCustomersCount
    {
        get => _totalCustomersCount;
        private set => SetProperty(ref _totalCustomersCount, value);
    }

    /// <summary>Count of active customers among the currently loaded list.</summary>
    public int ActiveCustomersCount
    {
        get => _activeCustomersCount;
        private set => SetProperty(ref _activeCustomersCount, value);
    }

    /// <summary>Active history filter: "all", "sale", or "quote".</summary>
    public string HistoryFilter
    {
        get => _historyFilter;
        set
        {
            if (SetProperty(ref _historyFilter, value))
            {
                RecomputeFilteredHistory();
            }
        }
    }

    /// <summary>Count of completed sales in the selected customer's history.</summary>
    public int HistoryPurchaseCount => HistoryEntries.Count(IsCompletedSale);

    /// <summary>Formatted sum of completed sales in the selected customer's history.</summary>
    public string HistoryTotalFormatted
        => $"${HistoryEntries.Where(IsCompletedSale).Sum(entry => entry.Amount).ToString("N0", ColombianCulture)}";

    /// <summary>Current outstanding customer debt from receivables, independent from the historical movement list.</summary>
    public decimal OutstandingBalance
    {
        get => _outstandingBalance;
        private set
        {
            if (SetProperty(ref _outstandingBalance, value))
            {
                OnPropertyChanged(nameof(OutstandingBalanceFormatted));
                OnPropertyChanged(nameof(HasOutstandingBalance));
                RaiseAbonarCanExecuteChanged();
            }
        }
    }

    public string OutstandingBalanceFormatted => $"${OutstandingBalance.ToString("N0", ColombianCulture)}";

    public bool HasOutstandingBalance => OutstandingBalance > 0m;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Guid? previouslySelectedId = SelectedCustomer?.Id;
        try
        {
            _isReloading = true;
            var results = await _searchCustomers.ExecuteAsync(new CustomerSearchRequest(SearchText, IncludeInactive), cancellationToken).ConfigureAwait(true);
            Customers.Clear();
            foreach (var customer in results)
            {
                Customers.Add(customer);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar clientes: {ex.Message}";
        }
        finally
        {
            _isReloading = false;
        }

        // Now that reloading is done, restore or clear selection
        if (previouslySelectedId.HasValue)
        {
            SelectedCustomer = Customers.FirstOrDefault(c => c.Id == previouslySelectedId.Value);
        }
        else
        {
            SelectedCustomer = null;
        }

        Message = Customers.Count == 0
            ? "No encontramos clientes con ese filtro. Puedes registrar uno nuevo."
            : IncludeInactive
                ? "Selecciona un cliente activo o inactivo para actualizarlo o reactivarlo."
                : "Selecciona un cliente para ver su historial o actualizar sus datos.";
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Guid? customerId = SelectedCustomer?.Id;
            await _saveCustomer.ExecuteAsync(new SaveCustomerRequest(customerId, FullName, Identification, Phone, Email, Address, IsActive), cancellationToken).ConfigureAwait(true);
            ClearForm();
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            Message = "Cliente guardado correctamente";
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

    /// <summary>
    /// "Eliminar" maps to a soft-delete (deactivate) via the existing save-customer use case: Domain has no
    /// hard-delete operation for customers, since sales/quote history can reference them.
    /// </summary>
    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        try
        {
            await _saveCustomer.ExecuteAsync(
                new SaveCustomerRequest(SelectedCustomer.Id, FullName, Identification, Phone, Email, Address, IsActive: false),
                cancellationToken).ConfigureAwait(true);
            ClearForm();
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            Message = "Cliente eliminado";
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

    public async Task OpenHistoryAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedCustomer is null)
        {
            Message = "Selecciona un cliente para ver su historial.";
            return;
        }

        try
        {
            Guid customerId = SelectedCustomer.Id;
            CustomerHistoryResult history = await _getHistory.ExecuteAsync(customerId, cancellationToken).ConfigureAwait(true);
            if (SelectedCustomer?.Id != customerId) return;

            OutstandingBalance = history.OutstandingBalance;
            HistoryEntries.Clear();
            foreach (CustomerHistoryEntry entry in history.Entries)
            {
                HistoryEntries.Add(entry);
            }

            await LoadOpenReceivablesAsync(customerId, cancellationToken).ConfigureAwait(true);
            if (SelectedCustomer?.Id != customerId) return;

            await LoadLoyaltyPointsAsync(customerId, cancellationToken).ConfigureAwait(true);
            if (SelectedCustomer?.Id != customerId) return;

            Message = HistoryEntries.Count == 0
                ? "Este cliente aún no tiene ventas o movimientos registrados."
                : $"Historial cargado: {HistoryEntries.Count} movimiento(s).";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar historial: {ex.Message}";
        }
    }

    /// <summary>
    /// Registers a payment (abono) against the selected receivable, then refreshes the history and balance
    /// so the SALDO reflects the reduced debt. The use case rejects zero amounts and overpayment.
    /// </summary>
    public async Task AbonarAsync(CancellationToken cancellationToken = default)
    {
        if (_applyPayment is null || SelectedReceivable is null || AbonoAmount <= 0m)
        {
            return;
        }

        try
        {
            Guid? cashierUserId = _session?.IsAuthenticated == true ? _session.OperatorId : null;
            Guid? shiftId = null;
            if (cashierUserId.HasValue && _cashierShifts is not null)
            {
                var openShift = await _cashierShifts.GetOpenShiftAsync(cashierUserId.Value, cancellationToken).ConfigureAwait(true);
                shiftId = openShift?.Id;
            }

            decimal abonado = AbonoAmount;
            await _applyPayment.ExecuteAsync(
                SelectedReceivable.ReceivableId,
                abonado,
                SelectedSettlementPaymentMethod.Method,
                string.IsNullOrWhiteSpace(SelectedSettlementBankOrPlatform) ? null : SelectedSettlementBankOrPlatform,
                cashierUserId,
                shiftId,
                cancellationToken).ConfigureAwait(true);
            AbonoAmount = 0m;
            await OpenHistoryAsync(cancellationToken).ConfigureAwait(true); // refresh SALDO + open receivables first
            Message = $"Abono de ${abonado.ToString("N0", ColombianCulture)} registrado.";
        }
        catch (ConcurrencyConflictException)
        {
            await OpenHistoryAsync(cancellationToken).ConfigureAwait(true);
            Message = "El saldo fue actualizado por otra operación. Se recargó el saldo; verifica el nuevo valor e inténtalo nuevamente.";
        }
        catch (InvalidOperationException ex)
        {
            // Overpayment or receivable-not-found: surface the clear domain message as-is.
            Message = ex.Message;
        }
    }

    public async Task PagarTodoSaldoAsync(CancellationToken cancellationToken = default)
    {
        if (_applyPayment is null || SelectedCustomer is null || OutstandingBalance <= 0m)
        {
            return;
        }

        try
        {
            Guid? cashierUserId = _session?.IsAuthenticated == true ? _session.OperatorId : null;
            Guid? shiftId = null;
            if (cashierUserId.HasValue && _cashierShifts is not null)
            {
                var openShift = await _cashierShifts.GetOpenShiftAsync(cashierUserId.Value, cancellationToken).ConfigureAwait(true);
                shiftId = openShift?.Id;
            }

            decimal abonado = await _applyPayment.ExecuteAllAsync(
                SelectedCustomer.Id,
                SelectedSettlementPaymentMethod.Method,
                string.IsNullOrWhiteSpace(SelectedSettlementBankOrPlatform) ? null : SelectedSettlementBankOrPlatform,
                cashierUserId,
                shiftId,
                cancellationToken).ConfigureAwait(true);
            AbonoAmount = 0m;
            await OpenHistoryAsync(cancellationToken).ConfigureAwait(true);
            Message = $"Saldo total de ${abonado.ToString("N0", ColombianCulture)} registrado.";
        }
        catch (ConcurrencyConflictException)
        {
            await OpenHistoryAsync(cancellationToken).ConfigureAwait(true);
            Message = "El saldo fue actualizado por otra operación. Se recargó el saldo; verifica el nuevo valor e inténtalo nuevamente.";
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }

    private void OpenInvoiceFromHistory(object? parameter)
    {
        if (parameter is not CustomerHistoryEntry entry || !CanOpenInvoiceFromHistory(entry) || entry.SaleId is null)
        {
            Message = "Selecciona una venta del historial para abrir su factura.";
            return;
        }

        InvoiceOpenRequested?.Invoke(entry.SaleId.Value);
    }

    private static bool CanOpenInvoiceFromHistory(object? parameter)
        => parameter is CustomerHistoryEntry entry && IsSaleKind(entry.Kind) && entry.SaleId.HasValue;

    public void ResetView()
    {
        ClearForm();
        Message = "Busca o selecciona un cliente para actualizar sus datos.";
    }

    private async Task LoadOpenReceivablesAsync(Guid customerId, CancellationToken cancellationToken)
    {
        OpenReceivables.Clear();
        SelectedReceivable = null;
        if (_receivableQuery is null)
        {
            return;
        }

        var open = await _receivableQuery.GetOpenReceivablesAsync(customerId, 1, 100, cancellationToken).ConfigureAwait(true);
        if (SelectedCustomer?.Id != customerId) return;

        foreach (ReceivableSummary receivable in open)
        {
            OpenReceivables.Add(receivable);
        }

        SelectedReceivable = OpenReceivables.FirstOrDefault();
    }

    private void RaiseAbonarCanExecuteChanged()
    {
        OnPropertyChanged(nameof(CanAbonar));
        OnPropertyChanged(nameof(CanPagarTodoSaldo));
        if (AbonarCommand is RelayCommand abonarCommand)
        {
            abonarCommand.RaiseCanExecuteChanged();
        }

        if (PagarTodoSaldoCommand is RelayCommand pagarTodoSaldoCommand)
        {
            pagarTodoSaldoCommand.RaiseCanExecuteChanged();
        }
    }

    private static bool TryParseAbonoAmount(string? input, out decimal amount)
    {
        amount = 0m;
        return !string.IsNullOrWhiteSpace(input)
            && decimal.TryParse(input.Trim(), NumberStyles.Number, ColombianCulture, out amount)
            && amount > 0m;
    }

    private async Task LoadLoyaltyPointsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        if (_getLoyaltyBalance is null)
        {
            LoyaltyPoints = 0;
            ApplyCurrentTier(null);
            return;
        }

        try
        {
            var balance = await _getLoyaltyBalance.ExecuteAsync(customerId, recentTransactionCount: 1, cancellationToken).ConfigureAwait(true);
            if (SelectedCustomer?.Id != customerId) return;

            LoyaltyPoints = balance?.Balance ?? 0;
            ApplyCurrentTier(balance?.CurrentTier);
        }
        catch (Exception ex)
        {
            if (SelectedCustomer?.Id != customerId) return;

            // Show the failure instead of silently reporting 0 points — a swallowed DB error
            // here looks identical to "customer has no points" and hides real defects.
            LoyaltyPoints = 0;
            ApplyCurrentTier(null);
            Message = $"Error al consultar puntos del cliente: {ex.Message}";
        }
    }

    private static bool IsSaleKind(string kind) => string.Equals(kind, "Sale", StringComparison.OrdinalIgnoreCase);

    private static bool IsCompletedSale(CustomerHistoryEntry entry)
        => IsSaleKind(entry.Kind) && string.Equals(entry.Status, "Completed", StringComparison.OrdinalIgnoreCase);

    private static bool IsQuoteKind(string kind) => string.Equals(kind, "Quote", StringComparison.OrdinalIgnoreCase);

    private void OnCustomersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        TotalCustomersCount = Customers.Count;
        ActiveCustomersCount = Customers.Count(customer => customer.IsActive);
    }

    private void OnHistoryEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RecomputeFilteredHistory();
        OnPropertyChanged(nameof(HistoryPurchaseCount));
        OnPropertyChanged(nameof(HistoryTotalFormatted));
    }

    private void RecomputeFilteredHistory()
    {
        FilteredHistoryEntries.Clear();
        IEnumerable<CustomerHistoryEntry> source = HistoryFilter switch
        {
            "sale" => HistoryEntries.Where(entry => IsSaleKind(entry.Kind)),
            "quote" => HistoryEntries.Where(entry => IsQuoteKind(entry.Kind)),
            _ => HistoryEntries
        };

        foreach (CustomerHistoryEntry entry in source)
        {
            FilteredHistoryEntries.Add(entry);
        }
    }

    private void ClearForm()
    {
        SelectedCustomer = null;
        FullName = string.Empty;
        Identification = string.Empty;
        Phone = null;
        Email = null;
        Address = null;
        IsActive = true;
        LoyaltyPoints = 0;
        ApplyCurrentTier(null);
        OutstandingBalance = 0m;
        HistoryEntries.Clear();
        OpenReceivables.Clear();
        SelectedReceivable = null;
        AbonoAmount = 0m;
        HistoryFilter = "all";
    }
}
