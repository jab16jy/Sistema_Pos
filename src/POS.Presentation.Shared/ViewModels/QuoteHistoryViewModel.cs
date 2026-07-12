using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Communication;
using POS.Application.DTOs.Quotes;
using POS.Application.DTOs.Sales;
using POS.Application.Interfaces;
using POS.Application.UseCases.Communication;
using POS.Application.UseCases.Quotes;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Presentation.Shared.Commands;
using POS.Presentation.Shared.Quotes;

namespace POS.Presentation.Shared.ViewModels;

public sealed class QuoteHistoryViewModel : ViewModelBase
{
    public const string SaleConversionConfirmationMessage = "¿Está seguro de transformar esta cotización en una venta?";

    private readonly GetQuoteHistoryUseCase _getHistoryUseCase;
    private readonly ConvertQuoteToSaleUseCase _convertUseCase;
    private readonly SendDocumentUseCase _sendUseCase;
    private readonly ICurrentOperatorSession? _operatorSession;
    private readonly IPdfService? _pdfService;
    private readonly GetQuoteDetailUseCase? _getQuoteDetail;
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
        SendDocumentUseCase sendUseCase,
        ICurrentOperatorSession? operatorSession = null,
        IPdfService? pdfService = null,
        GetQuoteDetailUseCase? getQuoteDetail = null)
    {
        _getHistoryUseCase = getHistoryUseCase ?? throw new ArgumentNullException(nameof(getHistoryUseCase));
        _convertUseCase = convertUseCase ?? throw new ArgumentNullException(nameof(convertUseCase));
        _sendUseCase = sendUseCase ?? throw new ArgumentNullException(nameof(sendUseCase));
        _operatorSession = operatorSession;
        _pdfService = pdfService;
        _getQuoteDetail = getQuoteDetail;

        Quotes = [];

        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => StatusMessage = $"Error al cargar cotizaciones: {ex.Message}"));
        NextPageCommand = new RelayCommand(_ => RunSafe(NextPageAsync(), ex => StatusMessage = $"Error al paginar cotizaciones: {ex.Message}"));
        PreviousPageCommand = new RelayCommand(_ => RunSafe(PreviousPageAsync(), ex => StatusMessage = $"Error al paginar cotizaciones: {ex.Message}"));
        ConvertToSaleCommand = new RelayCommand(p => RequestSaleConversion(p as QuoteSummaryRow ?? SelectedQuote));
        ExportPdfCommand = new RelayCommand(p => RunSafe(ExportPdfAsync(p as QuoteSummaryRow), ex => StatusMessage = $"Error al generar el PDF: {ex.Message}"));
        SendByEmailCommand = new RelayCommand(p => RunSafe(SendByEmailAsync(p as QuoteSummaryRow), ex => StatusMessage = $"Error al enviar el documento: {ex.Message}"));
        SendByWhatsAppCommand = new RelayCommand(p => RunSafe(SendByWhatsAppAsync(p as QuoteSummaryRow), ex => StatusMessage = $"Error al enviar el documento: {ex.Message}"));
        ViewDetailCommand = new RelayCommand(p => RunSafe(ViewDetailAsync(p as QuoteSummaryRow), ex => StatusMessage = $"Error al cargar el detalle de la cotización: {ex.Message}"));
    }

    public event EventHandler<(string FileName, byte[] Data)>? FileExportRequested;
    public event EventHandler<QuoteSummaryRow>? SaleConversionConfirmationRequested;
    public event EventHandler<QuoteDetailResult>? QuoteDetailRequested;

    public ObservableCollection<QuoteSummaryRow> Quotes { get; }

    public ICommand LoadCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public ICommand ConvertToSaleCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand SendByEmailCommand { get; }
    public ICommand SendByWhatsAppCommand { get; }
    public ICommand ViewDetailCommand { get; }

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
        private set
        {
            if (SetProperty(ref _statusMessage, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(IsStatusSuccess));
            }
        }
    }

    public bool IsStatusSuccess =>
        !string.IsNullOrEmpty(StatusMessage) &&
        !StatusMessage.StartsWith("Error", StringComparison.OrdinalIgnoreCase) &&
        !StatusMessage.StartsWith("No ", StringComparison.OrdinalIgnoreCase);

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
            SelectedQuote = null;
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

    /// <summary>Loads the full quote (line items included) and asks the view to display it — no PDF generated.</summary>
    public async Task ViewDetailAsync(QuoteSummaryRow? row, CancellationToken cancellationToken = default)
    {
        if (row is null)
        {
            return;
        }

        if (_getQuoteDetail is null)
        {
            StatusMessage = "El detalle de la cotización no está disponible en este entorno.";
            return;
        }

        SelectedQuote = row;

        QuoteDetailResult? detail = await _getQuoteDetail.ExecuteAsync(row.QuoteId, cancellationToken).ConfigureAwait(true);
        if (detail is null)
        {
            StatusMessage = "La cotización seleccionada ya no está disponible.";
            return;
        }

        QuoteDetailRequested?.Invoke(this, detail);
    }

    public async Task ExportPdfAsync(QuoteSummaryRow? row, CancellationToken cancellationToken = default)
    {
        if (row is null)
        {
            return;
        }

        if (_pdfService is null)
        {
            StatusMessage = "La generación de PDF no está disponible en este entorno.";
            return;
        }

        SelectedQuote = row;

        byte[] pdfBytes = await _pdfService.GenerateQuotePdfAsync(row.QuoteId, cancellationToken).ConfigureAwait(true);
        FileExportRequested?.Invoke(this, ($"{row.QuoteNumber}.pdf", pdfBytes));
        StatusMessage = $"PDF generado para {row.QuoteNumber}.";
    }

    /// <summary>
    /// Requests an explicit operator confirmation before creating a sale from the selected quote.
    /// </summary>
    public void RequestSaleConversion(QuoteSummaryRow? row)
    {
        if (row is null || !CanConvertToSale(row, ResolveCashierUserId()))
        {
            return;
        }

        SelectedQuote = row;
        SaleConversionConfirmationRequested?.Invoke(this, row);
    }

    /// <summary>
    /// Converts a quote only after the view has obtained operator confirmation.
    /// </summary>
    public Task ConfirmSaleConversionAsync(QuoteSummaryRow? row, CancellationToken cancellationToken = default)
        => ConvertToSaleAsync(row, ResolveCashierUserId(), null, cancellationToken);

    /// <summary>
    /// Converts an eligible (non-converted) quote to a sale using full cash payment.
    /// This explicit overload supports non-WPF callers that have already confirmed the operation.
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
        var payments = new[] { new CreateSalePaymentRequest(PaymentMethod.Cash, row?.Total ?? 0m, null, null) };
        await ConvertToSaleAsync(row, cashierUserId, payments, settlementPlan, "Cotización convertida", cancellationToken).ConfigureAwait(true);
    }

    private async Task ConvertToSaleAsync(
        QuoteSummaryRow? row,
        Guid cashierUserId,
        IReadOnlyList<CreateSalePaymentRequest> payments,
        SaleSettlementPlan? settlementPlan,
        string successMessagePrefix,
        CancellationToken cancellationToken = default)
    {
        if (row is null)
        {
            return;
        }

        if (!CanConvertToSale(row, cashierUserId))
        {
            return;
        }

        SelectedQuote = row;
        StatusMessage = "Convirtiendo cotización a venta...";

        try
        {
            CreateSaleResult result;
            var request = new Application.DTOs.Quotes.ConvertQuoteToSaleRequest(
                row.QuoteId,
                cashierUserId,
                payments);

            if (settlementPlan is not null)
            {
                result = await _convertUseCase.ExecuteAsync(request, settlementPlan, cancellationToken).ConfigureAwait(true);
            }
            else
            {
                result = await _convertUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
            }

            string successMessage = $"{successMessagePrefix}. Factura: {result.InvoiceNumber}.";
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            StatusMessage = successMessage;
        }
        catch (InsufficientStockException)
        {
            StatusMessage = "No se puede realizar la venta: no hay stock suficiente para uno o más productos de la cotización.";
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

    private Guid ResolveCashierUserId()
        => _operatorSession is { IsAuthenticated: true } ? _operatorSession.OperatorId : Guid.Empty;

    private bool CanConvertToSale(QuoteSummaryRow? row, Guid cashierUserId)
    {
        if (row is null)
        {
            return false;
        }

        if (!row.CanConvertToSale)
        {
            StatusMessage = row.Status == QuoteStatus.Converted
                ? "Esta cotización ya fue convertida a venta."
                : "Solo las cotizaciones activas o en borrador se pueden convertir a venta.";
            return false;
        }

        if (cashierUserId == Guid.Empty)
        {
            StatusMessage = "No hay cajero autenticado para convertir la cotización.";
            return false;
        }

        return true;
    }
}
