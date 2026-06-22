using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Communication;
using POS.Application.DTOs.Quotes;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Communication;
using POS.Application.UseCases.Quotes;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Presentation.Shared.Commands;
using POS.Presentation.Shared.Quotes;

namespace POS.Presentation.Shared.ViewModels;

public sealed class QuoteHistoryViewModel : ViewModelBase
{
    private readonly GetQuoteHistoryUseCase _getHistoryUseCase;
    private readonly ConvertQuoteToSaleUseCase _convertUseCase;
    private readonly SendDocumentUseCase _sendUseCase;
    private string _filterCustomerName = string.Empty;
    private QuoteStatus? _filterStatus;
    private DateOnly? _filterFrom;
    private DateOnly? _filterTo;
    private QuoteSummaryRow? _selectedQuote;
    private int _currentPage = 1;
    private int _totalCount;
    private int _pageSize = 20;
    private bool _isLoading;
    private string _statusMessage = string.Empty;

    public QuoteHistoryViewModel(
        GetQuoteHistoryUseCase getHistoryUseCase,
        ConvertQuoteToSaleUseCase convertUseCase,
        SendDocumentUseCase sendUseCase)
    {
        _getHistoryUseCase = getHistoryUseCase ?? throw new ArgumentNullException(nameof(getHistoryUseCase));
        _convertUseCase = convertUseCase ?? throw new ArgumentNullException(nameof(convertUseCase));
        _sendUseCase = sendUseCase ?? throw new ArgumentNullException(nameof(sendUseCase));

        Quotes = [];

        LoadCommand = new RelayCommand(async _ => await LoadAsync().ConfigureAwait(true));
        NextPageCommand = new RelayCommand(async _ => await NextPageAsync().ConfigureAwait(true));
        PreviousPageCommand = new RelayCommand(async _ => await PreviousPageAsync().ConfigureAwait(true));
        ConvertToSaleCommand = new RelayCommand(async p => await ConvertToSaleAsync(p as QuoteSummaryRow, Guid.Empty).ConfigureAwait(true));
        SendByEmailCommand = new RelayCommand(async p => await SendByEmailAsync(p as QuoteSummaryRow).ConfigureAwait(true));
        SendByWhatsAppCommand = new RelayCommand(async p => await SendByWhatsAppAsync(p as QuoteSummaryRow).ConfigureAwait(true));
    }

    public ObservableCollection<QuoteSummaryRow> Quotes { get; }

    public ICommand LoadCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public ICommand ConvertToSaleCommand { get; }
    public ICommand SendByEmailCommand { get; }
    public ICommand SendByWhatsAppCommand { get; }

    public string FilterCustomerName
    {
        get => _filterCustomerName;
        set => SetProperty(ref _filterCustomerName, value ?? string.Empty);
    }

    public QuoteStatus? FilterStatus
    {
        get => _filterStatus;
        set => SetProperty(ref _filterStatus, value);
    }

    public DateOnly? FilterFrom
    {
        get => _filterFrom;
        set => SetProperty(ref _filterFrom, value);
    }

    public DateOnly? FilterTo
    {
        get => _filterTo;
        set => SetProperty(ref _filterTo, value);
    }

    public QuoteSummaryRow? SelectedQuote
    {
        get => _selectedQuote;
        set => SetProperty(ref _selectedQuote, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set => SetProperty(ref _currentPage, value < 1 ? 1 : value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    public int PageSize
    {
        get => _pageSize;
        set => SetProperty(ref _pageSize, value < 1 ? 1 : value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value ?? string.Empty);
    }

    /// <summary>Loads the current page of quote history applying active filters.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StatusMessage = string.Empty;

        try
        {
            string? customerFilter = string.IsNullOrWhiteSpace(FilterCustomerName) ? null : FilterCustomerName;

            QuoteHistoryResult result = await _getHistoryUseCase.ExecuteAsync(
                FilterFrom,
                FilterTo,
                customerFilter,
                FilterStatus,
                CurrentPage,
                PageSize,
                cancellationToken).ConfigureAwait(true);

            Quotes.Clear();
            foreach (QuoteSummary summary in result.Items)
            {
                Quotes.Add(QuoteSummaryRow.FromSummary(summary));
            }

            TotalCount = result.TotalCount;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar cotizaciones: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Sends the quote by email using the customer's email address.
    /// Blocked when the row has no email — sets a recoverable validation message.
    /// </summary>
    public async Task SendByEmailAsync(QuoteSummaryRow? row, CancellationToken cancellationToken = default)
    {
        if (row is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(row.CustomerEmail))
        {
            StatusMessage = "No hay correo electrónico registrado para este cliente.";
            return;
        }

        SelectedQuote = row;

        try
        {
            SendDocumentRequest request = new(DocumentType.Quote, row.QuoteId, SendChannel.Email, row.CustomerEmail);
            SendDocumentResult result = await _sendUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

            StatusMessage = result.Success
                ? "Documento enviado correctamente vía email."
                : $"Error al enviar: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al enviar el documento: {ex.Message}";
        }
    }

    /// <summary>
    /// Sends the quote by WhatsApp using the customer's phone number.
    /// Blocked when the row has no phone — sets a recoverable validation message.
    /// </summary>
    public async Task SendByWhatsAppAsync(QuoteSummaryRow? row, CancellationToken cancellationToken = default)
    {
        if (row is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(row.CustomerPhone))
        {
            StatusMessage = "No hay número de teléfono registrado para este cliente.";
            return;
        }

        SelectedQuote = row;

        try
        {
            SendDocumentRequest request = new(DocumentType.Quote, row.QuoteId, SendChannel.WhatsApp, row.CustomerPhone);
            SendDocumentResult result = await _sendUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

            StatusMessage = result.Success
                ? "Documento enviado correctamente vía WhatsApp."
                : $"Error al enviar: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al enviar el documento: {ex.Message}";
        }
    }

    /// <summary>
    /// Converts an eligible (non-converted) quote to a sale using full cash payment.
    /// Blocked when the quote status is already Converted.
    /// </summary>
    public Task ConvertToSaleAsync(QuoteSummaryRow? row, Guid cashierUserId, CancellationToken cancellationToken = default)
        => ConvertToSaleAsync(row, cashierUserId, null, cancellationToken);

    /// <summary>
    /// Converts an eligible (non-converted) quote to a sale with an explicit settlement plan.
    /// Pass null for settlementPlan to use the legacy full-cash-payment path.
    /// Blocked when the quote status is already Converted.
    /// </summary>
    public async Task ConvertToSaleAsync(QuoteSummaryRow? row, Guid cashierUserId, SaleSettlementPlan? settlementPlan, CancellationToken cancellationToken = default)
    {
        if (row is null)
        {
            return;
        }

        if (row.Status == QuoteStatus.Converted)
        {
            StatusMessage = "Esta cotización ya fue convertida a venta.";
            return;
        }

        SelectedQuote = row;
        StatusMessage = "Convirtiendo cotización a venta...";

        try
        {
            CreateSaleResult result;
            if (settlementPlan is not null)
            {
                var request = new Application.DTOs.Quotes.ConvertQuoteToSaleRequest(
                    row.QuoteId,
                    cashierUserId,
                    [new CreateSalePaymentRequest(PaymentMethod.Cash, row.Total, null, null)]);
                result = await _convertUseCase.ExecuteAsync(request, settlementPlan, cancellationToken).ConfigureAwait(true);
            }
            else
            {
                // Legacy path: full cash payment for the quote total
                var request = new Application.DTOs.Quotes.ConvertQuoteToSaleRequest(
                    row.QuoteId,
                    cashierUserId,
                    [new CreateSalePaymentRequest(PaymentMethod.Cash, row.Total, null, null)]);
                result = await _convertUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
            }

            StatusMessage = $"Cotización convertida. Factura: {result.InvoiceNumber}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al convertir la cotización: {ex.Message}";
        }
    }

    private async Task NextPageAsync()
    {
        CurrentPage += 1;
        await LoadAsync().ConfigureAwait(true);
    }

    private async Task PreviousPageAsync()
    {
        if (CurrentPage <= 1)
        {
            return;
        }

        CurrentPage -= 1;
        await LoadAsync().ConfigureAwait(true);
    }
}
