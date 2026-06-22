using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Customers;
using POS.Application.UseCases.Customers;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class CustomersViewModel : ViewModelBase
{
    private readonly SaveCustomerUseCase _saveCustomer;
    private readonly GetCustomerHistoryUseCase _getHistory;
    private readonly SearchCustomersUseCase _searchCustomers;
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

    public CustomersViewModel(SaveCustomerUseCase saveCustomer, GetCustomerHistoryUseCase getHistory, SearchCustomersUseCase searchCustomers)
    {
        _saveCustomer = saveCustomer;
        _getHistory = getHistory;
        _searchCustomers = searchCustomers;
        LoadCommand = new RelayCommand(_ => _ = LoadAsync());
        SaveCommand = new RelayCommand(_ => _ = SaveAsync());
        OpenHistoryCommand = new RelayCommand(_ => _ = OpenHistoryAsync(), _ => SelectedCustomer is not null);
    }

    public ObservableCollection<CustomerSearchResult> Customers { get; } = [];

    public ObservableCollection<CustomerHistoryEntry> HistoryEntries { get; } = [];

    public ICommand LoadCommand { get; }

    public ICommand SaveCommand { get; }

    public ICommand OpenHistoryCommand { get; }

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
                }

                if (OpenHistoryCommand is RelayCommand command)
                {
                    command.RaiseCanExecuteChanged();
                }
            }
        }
    }

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

    public async Task LoadAsync(CancellationToken cancellationToken = default)
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

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Guid? customerId = SelectedCustomer?.Id;
            await _saveCustomer.ExecuteAsync(new SaveCustomerRequest(customerId, FullName, Identification, Phone, Email, Address, IsActive), cancellationToken).ConfigureAwait(true);
            ClearForm();
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            Message = "Cliente guardado. Ya está disponible para búsquedas y ventas.";
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

    private void ClearForm()
    {
        SelectedCustomer = null;
        FullName = string.Empty;
        Identification = string.Empty;
        Phone = null;
        Email = null;
        Address = null;
        IsActive = true;
    }
}
