using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Customers;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Loyalty;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class CustomersViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    private readonly SaveCustomerUseCase _saveCustomer;
    private readonly GetCustomerHistoryUseCase _getHistory;
    private readonly SearchCustomersUseCase _searchCustomers;
    private readonly GetLoyaltyBalanceUseCase? _getLoyaltyBalance;
    private CustomerSearchResult? _selectedCustomer;
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
    private int _totalCustomersCount;
    private int _activeCustomersCount;
    private string _historyFilter = "all";

    public CustomersViewModel(
        SaveCustomerUseCase saveCustomer,
        GetCustomerHistoryUseCase getHistory,
        SearchCustomersUseCase searchCustomers,
        GetLoyaltyBalanceUseCase? getLoyaltyBalance = null)
    {
        _saveCustomer = saveCustomer;
        _getHistory = getHistory;
        _searchCustomers = searchCustomers;
        _getLoyaltyBalance = getLoyaltyBalance;

        Customers.CollectionChanged += OnCustomersCollectionChanged;
        HistoryEntries.CollectionChanged += OnHistoryEntriesCollectionChanged;

        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => Message = $"Error al cargar clientes: {ex.Message}"));
        SaveCommand = new RelayCommand(_ => RunSafe(SaveAsync(), ex => Message = $"Error al guardar cliente: {ex.Message}"));
        DeleteCommand = new RelayCommand(_ => RunSafe(DeleteAsync(), ex => Message = $"Error al eliminar cliente: {ex.Message}"), _ => SelectedCustomer is not null);
        NewCommand = new RelayCommand(_ => ClearForm());
        OpenHistoryCommand = new RelayCommand(_ => RunSafe(OpenHistoryAsync(), ex => Message = $"Error al cargar historial: {ex.Message}"), _ => SelectedCustomer is not null);
        SetHistoryFilterCommand = new RelayCommand(filter => HistoryFilter = filter as string ?? "all");
    }

    public ObservableCollection<CustomerSearchResult> Customers { get; } = [];

    /// <summary>Full, unfiltered history for the selected customer. Kept for backward compatibility with existing consumers/tests.</summary>
    public ObservableCollection<CustomerHistoryEntry> HistoryEntries { get; } = [];

    /// <summary>History entries restricted by <see cref="HistoryFilter"/> ("all" / "sale" / "quote") for display.</summary>
    public ObservableCollection<CustomerHistoryEntry> FilteredHistoryEntries { get; } = [];

    public ICommand LoadCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand NewCommand { get; }

    public ICommand OpenHistoryCommand { get; }

    public ICommand SetHistoryFilterCommand { get; }

    public CustomerSearchResult? SelectedCustomer
    {
        get => _selectedCustomer;
        set
        {
            if (SetProperty(ref _selectedCustomer, value))
            {
                if (value is not null)
                {
                    FullName = value.FullName;
                    Identification = value.Identification;
                    Phone = value.Phone;
                    Email = value.Email;
                    Address = value.Address;
                    IsActive = value.IsActive;
                    RunSafe(LoadLoyaltyPointsAsync(value.Id), _ => LoyaltyPoints = 0);
                    RunSafe(OpenHistoryAsync(), ex => Message = $"Error al cargar historial: {ex.Message}");
                }
                else
                {
                    LoyaltyPoints = 0;
                    HistoryEntries.Clear();
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

    /// <summary>Count of sale-type entries in the selected customer's history.</summary>
    public int HistoryPurchaseCount => HistoryEntries.Count(entry => IsSaleKind(entry.Kind));

    /// <summary>Formatted sum of sale-type entries in the selected customer's history.</summary>
    public string HistoryTotalFormatted
        => $"${HistoryEntries.Where(entry => IsSaleKind(entry.Kind)).Sum(entry => entry.Amount).ToString("N0", ColombianCulture)}";

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await _searchCustomers.ExecuteAsync(new CustomerSearchRequest(SearchText, IncludeInactive), cancellationToken).ConfigureAwait(true);
            Customers.Clear();
            foreach (var customer in results)
            {
                Customers.Add(customer);
            }

            Message = Customers.Count == 0
                ? "No encontramos clientes con ese filtro. Puedes registrar uno nuevo."
                : IncludeInactive
                    ? "Selecciona un cliente activo o inactivo para actualizarlo o reactivarlo."
                    : "Selecciona un cliente para ver su historial o actualizar sus datos.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar clientes: {ex.Message}";
        }
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
            CustomerHistoryResult history = await _getHistory.ExecuteAsync(SelectedCustomer.Id, cancellationToken).ConfigureAwait(true);
            HistoryEntries.Clear();
            foreach (CustomerHistoryEntry entry in history.Entries)
            {
                HistoryEntries.Add(entry);
            }

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

    private async Task LoadLoyaltyPointsAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        if (_getLoyaltyBalance is null)
        {
            LoyaltyPoints = 0;
            return;
        }

        try
        {
            var balance = await _getLoyaltyBalance.ExecuteAsync(customerId, recentTransactionCount: 1, cancellationToken).ConfigureAwait(true);
            LoyaltyPoints = balance?.Balance ?? 0;
        }
        catch (Exception)
        {
            LoyaltyPoints = 0;
        }
    }

    private static bool IsSaleKind(string kind) => string.Equals(kind, "Sale", StringComparison.OrdinalIgnoreCase);

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
        HistoryFilter = "all";
    }
}
