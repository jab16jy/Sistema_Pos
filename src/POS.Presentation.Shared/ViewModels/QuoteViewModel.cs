using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Products;
using POS.Application.DTOs.Quotes;
using POS.Application.Interfaces;
using POS.Application.UseCases.Quotes;
using POS.Presentation.Shared.Commands;
using POS.Presentation.Shared.Quotes;

namespace POS.Presentation.Shared.ViewModels;

public sealed class QuoteViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");
    private readonly IProductLookupService _productLookupService;
    private readonly CreateQuoteUseCase _createQuoteUseCase;
    private string _searchQuery = string.Empty;
    private string _customerName = string.Empty;
    private string _customerPhone = string.Empty;
    private string _customerEmail = string.Empty;
    private int _validityDays = 30;
    private string _observations = string.Empty;
    private decimal _total;
    private string _totalFormatted = "$0";
    private string _validationMessage = string.Empty;
    private bool _isConfirming;
    private string _lastQuoteNumber = string.Empty;

    public QuoteViewModel(IProductLookupService productLookupService, CreateQuoteUseCase createQuoteUseCase)
    {
        _productLookupService = productLookupService ?? throw new ArgumentNullException(nameof(productLookupService));
        _createQuoteUseCase = createQuoteUseCase ?? throw new ArgumentNullException(nameof(createQuoteUseCase));

        SearchResults = [];
        CartItems = [];

        SearchCommand = new RelayCommand(async _ => await SearchAsync().ConfigureAwait(true));
        AddToCartCommand = new RelayCommand(p => AddToCart(p as ProductLookupResult));
        RemoveFromCartCommand = new RelayCommand(p => RemoveFromCart(p as QuoteCartItemViewModel));
        ConfirmCommand = new RelayCommand(async _ => await ConfirmAsync().ConfigureAwait(true));
    }

    public ObservableCollection<ProductLookupResult> SearchResults { get; }
    public ObservableCollection<QuoteCartItemViewModel> CartItems { get; }

    public ICommand SearchCommand { get; }
    public ICommand AddToCartCommand { get; }
    public ICommand RemoveFromCartCommand { get; }
    public ICommand ConfirmCommand { get; }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value ?? string.Empty);
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

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value ?? string.Empty);
    }

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
        IReadOnlyList<ProductLookupResult> results = await _productLookupService
            .SearchAsync(SearchQuery, cancellationToken)
            .ConfigureAwait(true);

        SearchResults.Clear();
        foreach (ProductLookupResult result in results)
        {
            SearchResults.Add(result);
        }
    }

    private void AddToCart(ProductLookupResult? product)
    {
        if (product is null)
        {
            return;
        }

        QuoteCartItemViewModel? existing = CartItems.FirstOrDefault(i => i.ProductId == product.ProductId);
        if (existing is not null)
        {
            existing.Quantity += 1m;
        }
        else
        {
            QuoteCartItemViewModel item = new(product.ProductId, product.Name, product.UnitPrice);
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

    private async Task ConfirmAsync(CancellationToken cancellationToken = default)
    {
        if (CartItems.Count == 0)
        {
            ValidationMessage = "Agrega al menos un producto a la cotización.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CustomerName))
        {
            ValidationMessage = "El nombre del cliente es obligatorio.";
            return;
        }

        IsConfirming = true;
        ValidationMessage = string.Empty;

        try
        {
            IReadOnlyCollection<CreateQuoteItemRequest> items = CartItems
                .Select(i => new CreateQuoteItemRequest(i.ProductId, i.Quantity, i.Discount))
                .ToList();

            CreateQuoteRequest request = new(
                null,
                CustomerName,
                string.IsNullOrWhiteSpace(CustomerPhone) ? null : CustomerPhone,
                string.IsNullOrWhiteSpace(CustomerEmail) ? null : CustomerEmail,
                ValidityDays,
                string.IsNullOrWhiteSpace(Observations) ? null : Observations,
                items);

            QuoteResult result = await _createQuoteUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

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

    private void RecalculateTotal()
    {
        Total = CartItems.Sum(i => i.Subtotal);
    }

    private void ClearForm()
    {
        CartItems.Clear();
        SearchResults.Clear();
        SearchQuery = string.Empty;
        CustomerName = string.Empty;
        CustomerPhone = string.Empty;
        CustomerEmail = string.Empty;
        ValidityDays = 30;
        Observations = string.Empty;
        RecalculateTotal();
    }
}
