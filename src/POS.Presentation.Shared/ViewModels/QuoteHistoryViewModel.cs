using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Communication;
using POS.Application.DTOs.Quotes;
using POS.Application.DTOs.Sales;
using POS.Application.Interfaces;
using POS.Application.UseCases.Communication;
using POS.Application.UseCases.Quotes;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Presentation.Shared.Commands;
using POS.Presentation.Shared.Quotes;

namespace POS.Presentation.Shared.ViewModels;

public sealed class QuoteHistoryViewModel : ViewModelBase
{
    public const string SaleConversionConfirmationMessage = "¿Está seguro de transformar esta cotización en una venta?";
    public const string CancelQuoteConfirmationMessage = "¿Está seguro de anular esta cotización?";

    private readonly GetQuoteHistoryUseCase _getHistoryUseCase;
    private readonly ConvertQuoteToSaleUseCase _convertUseCase;
    private readonly SendDocumentUseCase _sendUseCase;
    private readonly ICurrentOperatorSession? _operatorSession;
    private readonly IPdfService? _pdfService;
    private readonly GetQuoteDetailUseCase? _getQuoteDetail;
    private readonly CancelQuoteUseCase? _cancelQuoteUseCase;
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
        GetQuoteDetailUseCase? getQuoteDetail = null,
        CancelQuoteUseCase? cancelQuoteUseCase = null)
    {
        _getHistoryUseCase = getHistoryUseCase ?? throw new ArgumentNullException(nameof(getHistoryUseCase));
        _convertUseCase = convertUseCase ?? throw new ArgumentNullException(nameof(convertUseCase));
        _sendUseCase = sendUseCase ?? throw new ArgumentNullException(nameof(sendUseCase));
        _operatorSession = operatorSession;
        _pdfService = pdfService;
        _getQuoteDetail = getQuoteDetail;
        _cancelQuoteUseCase = cancelQuoteUseCase;

        Quotes = [];

        LoadCommand = new RelayCommand(_ => RunSafe(LoadAsync(), ex => StatusMessage = $"Error al cargar cotizaciones: {ex.Message}"));
        NextPageCommand = new RelayCommand(_ => RunSafe(NextPageAsync(), ex => StatusMessage = $"Error al paginar cotizaciones: {ex.Message}"));
        PreviousPageCommand = new RelayCommand(_ => RunSafe(PreviousPageAsync(), ex => StatusMessage = $"Error al paginar cotizaciones: {ex.Message}"));
        ConvertToSaleCommand = new RelayCommand(p => RequestSaleConversion(p as QuoteSummaryRow ?? SelectedQuote));
        CancelQuoteCommand = new RelayCommand(p => RequestQuoteCancellation(p as QuoteSummaryRow ?? SelectedQuote));
        ExportPdfCommand = new RelayCommand(p => RunSafe(ExportPdfAsync(p as QuoteSummaryRow), ex => StatusMessage = $"Error al generar el PDF: {ex.Message}"));
        SendByEmailCommand = new RelayCommand(p => RunSafe(SendByEmailAsync(p as QuoteSummaryRow), ex => StatusMessage = $"Error al enviar el documento: {ex.Message}"));
        SendByWhatsAppCommand = new RelayCommand(p => RunSafe(SendByWhatsAppAsync(p as QuoteSummaryRow), ex => StatusMessage = $"Error al enviar el documento: {ex.Message}"));
        ViewDetailCommand = new RelayCommand(p => RunSafe(ViewDetailAsync(p as QuoteSummaryRow), ex => StatusMessage = $"Error al cargar el detalle de la cotización: {ex.Message}"));
        EditCommand = new RelayCommand(p => RequestQuoteEdit(p as QuoteSummaryRow ?? SelectedQuote));
    }

    public event EventHandler<(string FileName, byte[] Data)>? FileExportRequested;
    public event EventHandler<QuoteSummaryRow>? SaleConversionConfirmationRequested;
    public event EventHandler<QuoteSummaryRow>? QuoteCancellationConfirmationRequested;
    public event EventHandler<QuoteDetailResult>? QuoteDetailRequested;
    public event EventHandler<QuoteDetailResult>? QuoteEditRequested;

    public ObservableCollection<QuoteSummaryRow> Quotes { get; }

    public ICommand LoadCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public ICommand ConvertToSaleCommand { get; }
    public ICommand CancelQuoteCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand SendByEmailCommand { get; }
    public ICommand SendByWhatsAppCommand { get; }
    public ICommand ViewDetailCommand { get; }
    public ICommand EditCommand { get; }

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

        if (!IsValidEmail(row.CustomerEmail))
        {
            StatusMessage = "El correo electrónico registrado no es válido.";
            return;
        }

        SelectedQuote = row;

        try
        {
            SendDocumentRequest request = new(DocumentType.Quote, row.QuoteId, SendChannel.Email, row.CustomerEmail);
            SendDocumentResult result = await _sendUseCase.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

            if (result.Success)
            {
                await LoadAsync(cancellationToken).ConfigureAwait(true);
                StatusMessage = "Documento enviado correctamente vía email.";
            }
            else
            {
                StatusMessage = $"Error al enviar: {result.ErrorMessage}";
            }
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

        if (!IsValidPhoneNumber(row.CustomerPhone))
        {
            StatusMessage = "El número de teléfono registrado no es válido (mínimo 8 dígitos).";
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

    /// <summary>
    /// Starts a correction of a quote that is still a proposal. Two independent gates apply and both
    /// report why they blocked instead of failing silently, mirroring <see cref="RequestQuoteCancellation"/>:
    /// the aggregate's status rule (<see cref="QuoteSummaryRow.CanEdit"/>) and the operator's permission.
    /// </summary>
    public void RequestQuoteEdit(QuoteSummaryRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (!row.CanEdit)
        {
            StatusMessage = $"La cotización '{row.QuoteNumber}' no se puede editar porque su estado es {row.StatusLabel}.";
            return;
        }

        if (_operatorSession is null || !_operatorSession.HasPermission(PermissionKeys.Quotes.Edit))
        {
            StatusMessage = "Tu rol no tiene permiso para editar cotizaciones.";
            return;
        }

        RunSafe(EditAsync(row), ex => StatusMessage = $"Error al cargar la cotización para editar: {ex.Message}");
    }

    /// <summary>
    /// Loads the full quote so the "Nueva cotización" form can be reused as the edit form — the same
    /// optional detail use case <see cref="ViewDetailAsync"/> uses, so a host without it degrades with a
    /// recoverable message instead of throwing.
    /// </summary>
    public async Task EditAsync(QuoteSummaryRow? row, CancellationToken cancellationToken = default)
    {
        if (row is null)
        {
            return;
        }

        if (_getQuoteDetail is null)
        {
            StatusMessage = "La edición de cotizaciones no está disponible en este entorno.";
            return;
        }

        SelectedQuote = row;

        QuoteDetailResult? detail = await _getQuoteDetail.ExecuteAsync(row.QuoteId, cancellationToken).ConfigureAwait(true);
        if (detail is null)
        {
            StatusMessage = "La cotización seleccionada ya no está disponible.";
            return;
        }

        QuoteEditRequested?.Invoke(this, detail);
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
        if (row is null || !CanConvertToSale(row))
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
        => ConvertToSaleAsync(row, null, cancellationToken);

    /// <summary>
    /// Converts an eligible (non-converted) quote to a sale using full cash payment.
    /// This explicit overload supports non-WPF callers that have already confirmed the operation.
    /// </summary>
    public Task ConvertToSaleAsync(QuoteSummaryRow? row, CancellationToken cancellationToken = default)
        => ConvertToSaleAsync(row, null, cancellationToken);

    /// <summary>
    /// Converts an eligible (non-converted) quote to a sale with an explicit settlement plan.
    /// Pass null for settlementPlan to use the legacy full-cash-payment path.
    /// Blocked when the quote status is already Converted.
    /// </summary>
    public async Task ConvertToSaleAsync(QuoteSummaryRow? row, SaleSettlementPlan? settlementPlan, CancellationToken cancellationToken = default)
    {
        var payments = new[] { new CreateSalePaymentRequest(PaymentMethod.Cash, row?.Total ?? 0m, null, null) };
        await ConvertToSaleAsync(row, payments, settlementPlan, "Cotización convertida", cancellationToken).ConfigureAwait(true);
    }

    private async Task ConvertToSaleAsync(
        QuoteSummaryRow? row,
        IReadOnlyList<CreateSalePaymentRequest> payments,
        SaleSettlementPlan? settlementPlan,
        string successMessagePrefix,
        CancellationToken cancellationToken = default)
    {
        if (row is null)
        {
            return;
        }

        if (!CanConvertToSale(row))
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

    private bool CanConvertToSale(QuoteSummaryRow? row)
    {
        if (row is null)
        {
            return false;
        }

        if (!row.CanConvertToSale)
        {
            StatusMessage = row.IsExpiredByCalendar
                ? "La cotización está vencida. Debes renovarla o crear otra cotización antes de vender."
                : row.Status == QuoteStatus.Converted
                ? "Esta cotización ya fue convertida a venta."
                : "Solo las cotizaciones activas o en borrador se pueden convertir a venta.";
            return false;
        }

        if (_operatorSession is not { IsAuthenticated: true })
        {
            StatusMessage = "No hay cajero autenticado para convertir la cotización.";
            return false;
        }

        return true;
    }

    public void RequestQuoteCancellation(QuoteSummaryRow? row)
    {
        if (row is null)
        {
            return;
        }

        if (!row.CanCancel)
        {
            StatusMessage = $"La cotización '{row.QuoteNumber}' no se puede anular porque su estado es {row.StatusLabel}.";
            return;
        }

        if (QuoteCancellationConfirmationRequested is { } handler)
        {
            handler.Invoke(this, row);
            return;
        }

        RunSafe(ConfirmQuoteCancellationAsync(row), ex => StatusMessage = $"Error al anular cotización: {ex.Message}");
    }

    public Task ConfirmQuoteCancellationAsync(QuoteSummaryRow row, CancellationToken cancellationToken = default)
        => CancelQuoteAsync(row, cancellationToken);

    public async Task CancelQuoteAsync(QuoteSummaryRow? row, CancellationToken cancellationToken = default)
    {
        if (row is null)
        {
            return;
        }

        if (!row.CanCancel)
        {
            StatusMessage = $"La cotización '{row.QuoteNumber}' no se puede anular porque su estado es {row.StatusLabel}.";
            return;
        }

        if (_cancelQuoteUseCase is null)
        {
            StatusMessage = "El servicio para anular cotizaciones no está disponible.";
            return;
        }

        await _cancelQuoteUseCase.ExecuteAsync(row.QuoteId, cancellationToken).ConfigureAwait(true);
        StatusMessage = $"Cotización '{row.QuoteNumber}' anulada correctamente.";
        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }
}
