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
    private readonly UpdateQuoteUseCase? _updateQuoteUseCase;
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
    private bool _isEditing;
    private Guid? _editingQuoteId;
    private string _lastQuoteNumber = string.Empty;
    private string _editedQuoteNumber = string.Empty;
    private decimal _cartDiscountPercentInput;
    private readonly RelayCommand _applyCartDiscountCommand;
    private readonly RelayCommand _clearCartDiscountCommand;
    private readonly RelayCommand _cancelEditCommand;

    public QuoteViewModel(
        IProductLookupService productLookupService,
        CreateQuoteUseCase createQuoteUseCase,
        SendDocumentUseCase? sendDocumentUseCase = null,
        SearchCustomersUseCase? searchCustomersUseCase = null,
        UpdateQuoteUseCase? updateQuoteUseCase = null)
    {
        _productLookupService = productLookupService ?? throw new ArgumentNullException(nameof(productLookupService));
        _createQuoteUseCase = createQuoteUseCase ?? throw new ArgumentNullException(nameof(createQuoteUseCase));
        _updateQuoteUseCase = updateQuoteUseCase;
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
        _cancelEditCommand = new RelayCommand(_ => CancelEdit(), _ => IsEditing);
        CancelEditCommand = _cancelEditCommand;
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
    public ICommand CancelEditCommand { get; }
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

    /// <summary>
    /// True while the form is correcting an existing quote instead of drafting a new one. Only the
    /// view-model knows which mode the confirm button is in, so the label follows it from here
    /// rather than being switched in the view.
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        private set
        {
            if (SetProperty(ref _isEditing, value))
            {
                OnPropertyChanged(nameof(ConfirmButtonLabel));
                // RelayCommand.CanExecuteChanged is a bare event with no CommandManager behind it, so WPF
                // evaluates CanExecute exactly once, at bind time. Without this the "Descartar" button is
                // bound while IsEditing is still false and never enables — the operator can enter edit mode
                // and then only leave it by saving. Same pattern as RecalculateTotal below.
                _cancelEditCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Quote number under edit; empty while the form is creating a new quote.</summary>
    public string EditedQuoteNumber
    {
        get => _editedQuoteNumber;
        private set => SetProperty(ref _editedQuoteNumber, value ?? string.Empty);
    }

    /// <summary>Verb-first label per pos-didactic-usability: the operator must never read a correction as a new proposal.</summary>
    public string ConfirmButtonLabel => IsEditing ? "Guardar cambios" : "Confirmar cotización";

    /// <summary>
    /// Reuses the create form to correct a quote that is still a proposal, so there is no second form to
    /// keep in sync. Hydrates every field the detail carries — including per-line quantity, discount and
    /// presentation — because a partial hydration would silently drop a correction the operator sees in
    /// the cart. Whether the quote may be edited at all was already decided upstream by the aggregate.
    /// </summary>
    /// <remarks>
    /// The cart is hydrated with the quote's FROZEN snapshot prices, not today's catalogue. That is
    /// deliberate: the save path deliberately re-resolves each line through <c>QuoteLineBuilder</c>
    /// (see the ODD's money-path rationale), so the form total and the persisted total can differ once
    /// the catalogue moves. <c>IProductLookupService</c> has no by-id lookup and this method is
    /// synchronous, so making the form total live would mean adding a product read per line and
    /// re-implementing presentation price resolution here — a second copy of exactly the resolution the
    /// shared builder exists to prevent drifting. Instead the divergence is made VISIBLE rather than
    /// silent: <see cref="UpdateQuoteAsync"/> replaces the total with the authoritative persisted one and
    /// says so, both in the form and in the success message. Making the form total live remains the
    /// better long-term fix and needs a by-id seam on <see cref="IProductLookupService"/>.
    /// </remarks>
    public void LoadForEdit(QuoteDetailResult detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        CartItems.Clear();
        SearchResults.Clear();
        CustomerSearchResults.Clear();
        SearchQuery = string.Empty;
        CartDiscountPercentInput = 0m;

        _editingQuoteId = detail.QuoteId;
        EditedQuoteNumber = detail.QuoteNumber;
        IsEditing = true;

        CustomerName = detail.CustomerName;
        CustomerPhone = detail.CustomerPhone ?? string.Empty;
        CustomerEmail = detail.CustomerEmail ?? string.Empty;
        SelectedCustomerId = detail.CustomerId;
        // No identification in the detail payload, so the badge states the link without inventing one.
        SelectedCustomerLabel = detail.CustomerId.HasValue ? $"Cliente registrado: {detail.CustomerName}" : string.Empty;
        ValidityDays = detail.ValidityDays;
        Observations = detail.Observations ?? string.Empty;

        foreach (QuoteDetailItemResult line in detail.Items)
        {
            // A detail payload carries no stock, and quoting never validates it. Passing the line's own
            // inventory demand as the ceiling keeps the "no stock" hint silent instead of inventing an
            // out-of-stock warning for a large quantity.
            QuoteCartItemViewModel item = new(
                line.ProductId,
                line.ProductName,
                line.UnitPrice,
                availableStock: line.Quantity * line.UnitsPerPresentation,
                line.PresentationName,
                line.UnitsPerPresentation)
            {
                Quantity = line.Quantity,
                Discount = line.Discount
            };
            item.PropertyChanged += (_, _) => RecalculateTotal();
            CartItems.Add(item);
        }

        RecalculateTotal();
        ValidationMessage = $"Estás editando la cotización {detail.QuoteNumber}. Al guardar conserva el mismo número.";
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
            if (IsEditing)
            {
                await UpdateQuoteAsync(cancellationToken).ConfigureAwait(true);
                return;
            }

            QuoteResult? result = await CreateQuoteAsync(cancellationToken).ConfigureAwait(true);
            if (result is null)
            {
                return;
            }

            LastQuoteNumber = result.QuoteNumber;
            ValidationMessage = $"Cotización {result.QuoteNumber} creada correctamente. Total: {result.Total.ToString("N0", ColombianCulture)}.";
            ClearForm();
            // After ClearForm for the same reason as UpdateQuoteAsync: the empty cart recalculates to
            // zero, and zero is not the number this quote is worth.
            Total = result.Total;
        }
        catch (Exception ex)
        {
            ValidationMessage = IsEditing
                ? $"Error al guardar los cambios de la cotización: {ex.Message}"
                : $"Error al crear la cotización: {ex.Message}";
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
            // Sending while editing must persist the correction first: taking the create path here would
            // burn a second quote number for the same proposal and send that one instead of the corrected one.
            QuoteResult? result = IsEditing
                ? await UpdateQuoteAsync(cancellationToken).ConfigureAwait(true)
                : await CreateQuoteAsync(cancellationToken).ConfigureAwait(true);
            if (result is null)
            {
                return;
            }

            SendDocumentRequest request = new(DocumentType.Quote, result.QuoteId, channel, recipient);
            SendDocumentResult sendResult = await _sendDocumentUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

            LastQuoteNumber = result.QuoteNumber;
            // Same reason as UpdateQuoteAsync: the persisted total is the only authoritative figure,
            // and this message is the last thing the operator reads before the quote goes out to a
            // client. Stating the amount is what makes a later re-pricing a visible change.
            string totalText = result.Total.ToString("N0", ColombianCulture);
            ValidationMessage = sendResult.Success
                ? $"Cotización {result.QuoteNumber} enviada correctamente. Total: ${totalText}."
                : $"Cotización {result.QuoteNumber} creada, pero no se pudo enviar: {sendResult.ErrorMessage}";
            ClearForm();
            Total = result.Total;
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
    /// Abandons an edit without saving. Without this, entering edit mode is a one-way door the operator
    /// can only leave by saving corrections they may not have wanted to make.
    /// </summary>
    public void CancelEdit()
    {
        if (!IsEditing)
        {
            return;
        }

        ClearForm();
    }

    /// <summary>
    /// Saves the hydrated form over the quote being corrected. Returns null without writing when the form
    /// is still incomplete, so the operator's corrections stay on screen instead of vanishing. The lines
    /// reuse <see cref="CreateQuoteItemRequest"/> exactly as creation does, which is what routes the edit
    /// through the same discount ceiling and price resolution the quote it replaces went through.
    /// </summary>
    /// <remarks>
    /// The use case's returned <c>Total</c> is the persisted figure and the only authoritative one — the
    /// quote re-resolves every line price from the current catalogue, so the hydrated form total is a
    /// frozen snapshot that may no longer match. It used to be discarded here, which meant the operator
    /// approved a number that was not the number stored, the success message carried no amount at all,
    /// and the higher re-priced total then rode on to the invoice at conversion. Now the persisted total
    /// is written back into the form and stated in the message, and when it differs from what the operator
    /// was looking at, the message says so explicitly instead of letting the change pass unnoticed.
    /// </remarks>
    private async Task<QuoteResult?> UpdateQuoteAsync(CancellationToken cancellationToken)
    {
        if (_updateQuoteUseCase is null)
        {
            ValidationMessage = "La edición de cotizaciones no está disponible en este entorno.";
            return null;
        }

        if (_editingQuoteId is not { } quoteId)
        {
            ValidationMessage = "No hay ninguna cotización en edición. Vuelve al historial y elige Editar.";
            return null;
        }

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

        UpdateQuoteRequest request = new(
            quoteId,
            CustomerName,
            // The picker stays live in edit mode, so this carries whatever is currently selected —
            // including a re-link to a different customer, or null to drop back to a walk-in quote.
            SelectedCustomerId,
            string.IsNullOrWhiteSpace(CustomerPhone) ? null : CustomerPhone,
            string.IsNullOrWhiteSpace(CustomerEmail) ? null : CustomerEmail,
            ValidityDays,
            string.IsNullOrWhiteSpace(Observations) ? null : Observations,
            items);

        decimal formTotalBeforeSave = Total;
        QuoteResult result = await _updateQuoteUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

        LastQuoteNumber = result.QuoteNumber;
        // ClearForm drops the edit identity too, so the operator lands back in create mode — and the
        // updated number survives on screen because ClearForm deliberately leaves LastQuoteNumber alone.
        ClearForm();

        // Set AFTER ClearForm on purpose: ClearForm recalculates the (now empty) cart to zero, and the
        // operator's last meaningful figure for this quote is the total that was actually stored, not
        // the empty cart. This is the same reason LastQuoteNumber is left alone by ClearForm.
        Total = result.Total;

        bool totalChanged = decimal.Round(result.Total, 2) != decimal.Round(formTotalBeforeSave, 2);
        string formattedTotal = TotalFormatted;
        ValidationMessage = totalChanged
            ? $"Cotización {result.QuoteNumber} actualizada correctamente. Total guardado: {formattedTotal}. " +
              $"El precio del catálogo cambió desde que abriste la cotización (el formulario mostraba " +
              $"{formTotalBeforeSave.ToString("N0", ColombianCulture)})."
            : $"Cotización {result.QuoteNumber} actualizada correctamente. Total: {formattedTotal}.";
        return result;
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
        _editingQuoteId = null;
        EditedQuoteNumber = string.Empty;
        IsEditing = false;
        RecalculateTotal();
    }
}
