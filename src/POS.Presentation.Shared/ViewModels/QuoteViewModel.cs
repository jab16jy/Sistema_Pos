using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Customers;
using POS.Application.DTOs.Communication;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Quotes;
using POS.Application.Interfaces;
using POS.Application.UseCases.Communication;
using POS.Application.UseCases.Customers;
using POS.Application.UseCases.Quotes;
using POS.Domain.Enums;
using POS.Presentation.Shared.Cashier;
using POS.Presentation.Shared.Commands;
using POS.Presentation.Shared.Quotes;

namespace POS.Presentation.Shared.ViewModels;

public sealed class QuoteViewModel : ViewModelBase
{
    private const int AutocompleteDelayMilliseconds = 180;
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");
    private readonly IProductLookupService _productLookupService;
    private readonly CreateQuoteUseCase _createQuoteUseCase;
    private readonly SendDocumentUseCase? _sendDocumentUseCase;
    private readonly SearchCustomersUseCase? _searchCustomersUseCase;
    private CancellationTokenSource? _autocompleteSearchCts;
    private string _searchQuery = string.Empty;
    private string _customerName = string.Empty;
    private string _customerPhone = string.Empty;
    private string _customerEmail = string.Empty;
    private Guid? _selectedCustomerId;
    private string _selectedCustomerLabel = string.Empty;
    private int _validityDays = 30;
    private string _observations = string.Empty;
    private decimal _total;
    private string _totalFormatted = "$0";
    private string _validationMessage = string.Empty;
    private bool _isConfirming;
    private string _lastQuoteNumber = string.Empty;
    private decimal _cartDiscountPercentInput;
    private readonly RelayCommand _applyCartDiscountCommand;
    private readonly RelayCommand _clearCartDiscountCommand;

    public QuoteViewModel(
        IProductLookupService productLookupService,
        CreateQuoteUseCase createQuoteUseCase,
        SendDocumentUseCase? sendDocumentUseCase = null,
        SearchCustomersUseCase? searchCustomersUseCase = null)
    {
        _productLookupService = productLookupService ?? throw new ArgumentNullException(nameof(productLookupService));
        _createQuoteUseCase = createQuoteUseCase ?? throw new ArgumentNullException(nameof(createQuoteUseCase));
        _sendDocumentUseCase = sendDocumentUseCase;
        _searchCustomersUseCase = searchCustomersUseCase;

        SearchResults = [];
        CustomerSearchResults = [];
        CartItems = [];

        SearchCommand = new RelayCommand(_ => RunSafe(SearchAsync(), ex => ValidationMessage = $"Error al buscar productos: {ex.Message}"));
        SearchCustomerCommand = new RelayCommand(_ => RunSafe(SearchCustomersAsync(), ex => ValidationMessage = $"Error al buscar clientes: {ex.Message}"));
        SelectCustomerCommand = new RelayCommand(p => SelectCustomer(p as CustomerSearchResult));
        AddToCartCommand = new RelayCommand(p => {
            if (p is PresentationOptionViewModel opt)
                AddToCart(opt.Product, opt.Name);
            else if (p is CashierProductSearchResultViewModel prod)
                AddToCart(prod, null);
        });
        AddUnitToCartCommand = new RelayCommand(p => AddToCart(p as CashierProductSearchResultViewModel, null));
        AddBoxToCartCommand = new RelayCommand(p => AddToCart(p as CashierProductSearchResultViewModel, "Caja"));
        RemoveFromCartCommand = new RelayCommand(p => RemoveFromCart(p as QuoteCartItemViewModel));
        _applyCartDiscountCommand = new RelayCommand(_ => ApplyCartDiscount(), _ => CartItems.Count > 0);
        _clearCartDiscountCommand = new RelayCommand(_ => ClearCartDiscount(), _ => HasManualDiscount);
        ApplyCartDiscountCommand = _applyCartDiscountCommand;
        ClearCartDiscountCommand = _clearCartDiscountCommand;
        ConfirmCommand = new RelayCommand(_ => RunSafe(ConfirmAsync(), ex => ValidationMessage = $"Error al crear la cotización: {ex.Message}"));
        SendByEmailCommand = new RelayCommand(_ => RunSafe(SendByEmailAsync(), ex => ValidationMessage = $"Error al enviar la cotización: {ex.Message}"));
        SendByWhatsAppCommand = new RelayCommand(_ => RunSafe(SendByWhatsAppAsync(), ex => ValidationMessage = $"Error al enviar la cotización: {ex.Message}"));
    }

    public ObservableCollection<CashierProductSearchResultViewModel> SearchResults { get; }
    public ObservableCollection<CustomerSearchResult> CustomerSearchResults { get; }
    public ObservableCollection<QuoteCartItemViewModel> CartItems { get; }

    public ICommand SearchCommand { get; }
    public ICommand SearchCustomerCommand { get; }
    public ICommand SelectCustomerCommand { get; }
    public ICommand AddToCartCommand { get; }
    public ICommand AddUnitToCartCommand { get; }
    public ICommand AddBoxToCartCommand { get; }
    public ICommand RemoveFromCartCommand { get; }
    public ICommand ApplyCartDiscountCommand { get; }
    public ICommand ClearCartDiscountCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand SendByEmailCommand { get; }
    public ICommand SendByWhatsAppCommand { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value ?? string.Empty))
            {
                StartAutocompleteSearch();
            }
        }
    }

    public string CustomerName
    {
        get => _customerName;
        set => SetProperty(ref _customerName, value ?? string.Empty);
    }

    public string CustomerPhone
    {
        get => _customerPhone;
        set => SetProperty(ref _customerPhone, value ?? string.Empty);
    }

    public string CustomerEmail
    {
        get => _customerEmail;
        set => SetProperty(ref _customerEmail, value ?? string.Empty);
    }

    public Guid? SelectedCustomerId
    {
        get => _selectedCustomerId;
        private set
        {
            if (SetProperty(ref _selectedCustomerId, value))
            {
                OnPropertyChanged(nameof(HasSelectedCustomer));
            }
        }
    }

    public bool HasSelectedCustomer => SelectedCustomerId.HasValue;

    public string SelectedCustomerLabel
    {
        get => _selectedCustomerLabel;
        private set => SetProperty(ref _selectedCustomerLabel, value ?? string.Empty);
    }

    public int ValidityDays
    {
        get => _validityDays;
        set => SetProperty(ref _validityDays, value < 1 ? 1 : value);
    }

    public string Observations
    {
        get => _observations;
        set => SetProperty(ref _observations, value ?? string.Empty);
    }

    public decimal Total
    {
        get => _total;
        private set
        {
            if (SetProperty(ref _total, value))
            {
                TotalFormatted = $"${value.ToString("N0", ColombianCulture)}";
            }
        }
    }

    public string TotalFormatted
    {
        get => _totalFormatted;
        private set => SetProperty(ref _totalFormatted, value);
    }

    /// <summary>Percentage typed by the user, applied to every cart line via ApplyCartDiscountCommand.</summary>
    public decimal CartDiscountPercentInput
    {
        get => _cartDiscountPercentInput;
        set => SetProperty(ref _cartDiscountPercentInput, Math.Clamp(value, 0m, 100m));
    }

    /// <summary>True when any cart line currently carries a manual discount.</summary>
    public bool HasManualDiscount => CartItems.Any(item => item.Discount > 0m);

    public string ValidationMessage
    {
        get => _validationMessage;
        private set
        {
            if (SetProperty(ref _validationMessage, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(IsValidationSuccess));
            }
        }
    }

    public bool IsValidationSuccess =>
        !string.IsNullOrEmpty(ValidationMessage) &&
        !ValidationMessage.StartsWith("Error", StringComparison.OrdinalIgnoreCase) &&
        !ValidationMessage.StartsWith("Ingresa", StringComparison.OrdinalIgnoreCase) &&
        !ValidationMessage.StartsWith("No ", StringComparison.OrdinalIgnoreCase);

    public bool IsConfirming
    {
        get => _isConfirming;
        private set => SetProperty(ref _isConfirming, value);
    }

    public string LastQuoteNumber
    {
        get => _lastQuoteNumber;
        private set => SetProperty(ref _lastQuoteNumber, value ?? string.Empty);
    }

    private async Task SearchAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            CancelAutocompleteSearch();
            await SearchProductsAsync(SearchQuery, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ValidationMessage = $"Error al buscar productos: {ex.Message}";
        }
    }

    /// <summary>Debounces product search as the user types, mirroring the cashier's autocomplete search.</summary>
    private void StartAutocompleteSearch()
    {
        CancelAutocompleteSearch();

        if (SearchQuery.Trim().Length == 0)
        {
            SearchResults.Clear();
            return;
        }

        _autocompleteSearchCts = new CancellationTokenSource();
        CancellationToken token = _autocompleteSearchCts.Token;
        RunSafe(RunAutocompleteSearchAsync(SearchQuery, token), ex => ValidationMessage = $"Error al buscar productos: {ex.Message}");
    }

    private async Task RunAutocompleteSearchAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(AutocompleteDelayMilliseconds, cancellationToken).ConfigureAwait(true);
            await SearchProductsAsync(query, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SearchProductsAsync(string query, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProductLookupResult> results = await _productLookupService
            .SearchAsync(query, cancellationToken)
            .ConfigureAwait(true);

        if (cancellationToken.IsCancellationRequested || !string.Equals(SearchQuery.Trim(), query.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SearchResults.Clear();
        foreach (ProductLookupResult result in results)
        {
            SearchResults.Add(new CashierProductSearchResultViewModel(result));
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

    private async Task SearchCustomersAsync(CancellationToken cancellationToken = default)
    {
        if (_searchCustomersUseCase is null)
        {
            ValidationMessage = "La búsqueda de clientes no está disponible en este entorno.";
            return;
        }

        try
        {
            IReadOnlyList<CustomerSearchResult> results = await _searchCustomersUseCase
                .ExecuteAsync(new CustomerSearchRequest(CustomerName, IncludeInactive: false), cancellationToken)
                .ConfigureAwait(true);

            CustomerSearchResults.Clear();
            foreach (CustomerSearchResult result in results)
            {
                CustomerSearchResults.Add(result);
            }

            ValidationMessage = results.Count == 0
                ? "No encontramos clientes activos con ese filtro. Puedes continuar como cliente ocasional."
                : "Selecciona un cliente registrado para prellenar la cotización.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ValidationMessage = $"Error al buscar clientes: {ex.Message}";
        }
    }

    private void SelectCustomer(CustomerSearchResult? customer)
    {
        if (customer is null)
        {
            return;
        }

        SelectedCustomerId = customer.Id;
        SelectedCustomerLabel = $"Cliente registrado: {customer.FullName} · ID {customer.Identification}";
        CustomerName = customer.FullName;
        CustomerPhone = customer.Phone ?? string.Empty;
        CustomerEmail = customer.Email ?? string.Empty;
        CustomerSearchResults.Clear();
        ValidationMessage = "Cliente registrado vinculado a la cotización.";
    }

    public void AddToCart(CashierProductSearchResultViewModel? product, string? presentationName = null)
    {
        if (product is null)
        {
            return;
        }

        (int Units, decimal Price)? package = product.PackageFor(presentationName);
        if (!product.Supports(presentationName))
        {
            ValidationMessage = $"{product.Name} no está configurado para la presentación '{presentationName}'.";
            return;
        }

        int unitsPerPresentation = package?.Units ?? 1;
        decimal unitPrice = package?.Price ?? product.UnitPrice;
        QuoteCartItemViewModel? existing = CartItems.FirstOrDefault(i =>
            i.ProductId == product.ProductId && string.Equals(i.PresentationName, presentationName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Quantity += 1m;
        }
        else
        {
            QuoteCartItemViewModel item = new(
                product.ProductId,
                product.Name,
                unitPrice,
                product.AvailableStock,
                presentationName,
                unitsPerPresentation);
            item.PropertyChanged += (_, _) => RecalculateTotal();
            CartItems.Add(item);
        }

        RecalculateTotal();
    }

    private void RemoveFromCart(QuoteCartItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        CartItems.Remove(item);
        RecalculateTotal();
    }

    public async Task ConfirmAsync(CancellationToken cancellationToken = default)
    {
        IsConfirming = true;
        ValidationMessage = string.Empty;

        try
        {
            QuoteResult? result = await CreateQuoteAsync(cancellationToken).ConfigureAwait(true);
            if (result is null)
            {
                return;
            }

            LastQuoteNumber = result.QuoteNumber;
            ValidationMessage = $"Cotización {result.QuoteNumber} creada correctamente.";
            ClearForm();
        }
        catch (Exception ex)
        {
            ValidationMessage = $"Error al crear la cotización: {ex.Message}";
        }
        finally
        {
            IsConfirming = false;
        }
    }

    /// <summary>
    /// Sends the in-progress quote by email. The wireframe's mock JS sends an unsaved draft directly;
    /// the real backend only sends persisted documents (SendDocumentUseCase requires a QuoteId), so this
    /// creates the quote first (reusing the existing validation/creation path) and immediately sends it —
    /// a deliberate, documented deviation from the literal wireframe mock.
    /// </summary>
    public async Task SendByEmailAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CustomerEmail))
        {
            ValidationMessage = "Ingresa el email del cliente";
            return;
        }

        if (!IsValidEmail(CustomerEmail))
        {
            ValidationMessage = "Ingresa un correo electrónico válido";
            return;
        }

        await CreateAndSendAsync(SendChannel.Email, CustomerEmail, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Sends the in-progress quote by WhatsApp. See <see cref="SendByEmailAsync"/> for the create-then-send rationale.</summary>
    public async Task SendByWhatsAppAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(CustomerPhone))
        {
            ValidationMessage = "Ingresa el teléfono del cliente";
            return;
        }

        if (!IsValidPhoneNumber(CustomerPhone))
        {
            ValidationMessage = "Ingresa un número de teléfono válido (mínimo 8 dígitos)";
            return;
        }

        await CreateAndSendAsync(SendChannel.WhatsApp, CustomerPhone, cancellationToken).ConfigureAwait(true);
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                email.Trim(),
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(250));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidPhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return false;
        }

        string digits = new(phone.Where(char.IsDigit).ToArray());
        return digits.Length >= 8;
    }

    private async Task CreateAndSendAsync(SendChannel channel, string recipient, CancellationToken cancellationToken)
    {
        if (_sendDocumentUseCase is null)
        {
            ValidationMessage = "El envío de documentos no está disponible en este entorno.";
            return;
        }

        IsConfirming = true;
        ValidationMessage = string.Empty;

        try
        {
            QuoteResult? result = await CreateQuoteAsync(cancellationToken).ConfigureAwait(true);
            if (result is null)
            {
                return;
            }

            SendDocumentRequest request = new(DocumentType.Quote, result.QuoteId, channel, recipient);
            SendDocumentResult sendResult = await _sendDocumentUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

            LastQuoteNumber = result.QuoteNumber;
            ValidationMessage = sendResult.Success
                ? $"Cotización {result.QuoteNumber} enviada correctamente."
                : $"Cotización {result.QuoteNumber} creada, pero no se pudo enviar: {sendResult.ErrorMessage}";
            ClearForm();
        }
        catch (Exception ex)
        {
            ValidationMessage = $"Error al enviar la cotización: {ex.Message}";
        }
        finally
        {
            IsConfirming = false;
        }
    }

    private async Task<QuoteResult?> CreateQuoteAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(CustomerName))
        {
            ValidationMessage = "Ingresa el nombre del cliente";
            return null;
        }

        if (CartItems.Count == 0)
        {
            ValidationMessage = "Agrega al menos un producto";
            return null;
        }

        IReadOnlyCollection<CreateQuoteItemRequest> items = CartItems
            .Select(i => new CreateQuoteItemRequest(i.ProductId, i.Quantity, i.Discount, i.PresentationName))
            .ToList();

        CreateQuoteRequest request = new(
            SelectedCustomerId,
            CustomerName,
            string.IsNullOrWhiteSpace(CustomerPhone) ? null : CustomerPhone,
            string.IsNullOrWhiteSpace(CustomerEmail) ? null : CustomerEmail,
            ValidityDays,
            string.IsNullOrWhiteSpace(Observations) ? null : Observations,
            items);

        return await _createQuoteUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
    }

    /// <summary>
    /// Applies CartDiscountPercentInput to every cart line at once — more ergonomic than editing
    /// lines one by one. Mirrors CashierViewModel.ApplyCartDiscount; there is no separate cart-level
    /// discount field, this is purely a bulk-set convenience over QuoteCartItemViewModel.Discount.
    /// </summary>
    private void ApplyCartDiscount()
    {
        foreach (QuoteCartItemViewModel item in CartItems)
        {
            item.Discount = CartDiscountPercentInput;
        }

        ValidationMessage = CartDiscountPercentInput > 0m
            ? $"Descuento de {CartDiscountPercentInput.ToString("0.##", ColombianCulture)}% aplicado al carrito."
            : "Descuento removido del carrito.";
        RecalculateTotal();
    }

    private void ClearCartDiscount()
    {
        CartDiscountPercentInput = 0m;
        foreach (QuoteCartItemViewModel item in CartItems)
        {
            item.Discount = 0m;
        }

        ValidationMessage = "Descuento removido del carrito.";
        RecalculateTotal();
    }

    private void RecalculateTotal()
    {
        Total = CartItems.Sum(i => i.Subtotal);
        OnPropertyChanged(nameof(HasManualDiscount));
        _applyCartDiscountCommand.RaiseCanExecuteChanged();
        _clearCartDiscountCommand.RaiseCanExecuteChanged();
    }

    private void ClearForm()
    {
        CartItems.Clear();
        SearchResults.Clear();
        CustomerSearchResults.Clear();
        SearchQuery = string.Empty;
        CustomerName = string.Empty;
        CustomerPhone = string.Empty;
        CustomerEmail = string.Empty;
        SelectedCustomerId = null;
        SelectedCustomerLabel = string.Empty;
        ValidityDays = 30;
        Observations = string.Empty;
        CartDiscountPercentInput = 0m;
        RecalculateTotal();
    }
}
