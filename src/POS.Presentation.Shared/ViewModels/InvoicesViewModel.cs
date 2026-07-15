using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.IO;
using POS.Application.DTOs.Communication;
using POS.Application.DTOs.Invoices;
using POS.Application.Interfaces;
using POS.Application.UseCases.Communication;
using POS.Application.UseCases.Invoices;
using POS.Domain.Authorization;
using POS.Domain.Exceptions;
using POS.Domain.Enums;
using POS.Presentation.Shared.Commands;
using POS.Presentation.Shared.Invoices;

namespace POS.Presentation.Shared.ViewModels;

public sealed class InvoicesViewModel : ViewModelBase
{
    private const int PageSize = 50;

    /// <summary>Fixed return-reason options matching the wireframe's <c>#return-reason</c> select.</summary>
    public static IReadOnlyList<string> ReturnReasonOptions { get; } =
    [
        "Producto dañado o defectuoso",
        "Producto incorrecto",
        "Cliente se arrepintió",
        "Vencimiento próximo",
        "Otro"
    ];

    /// <summary>
    /// Fixed refund-type options matching the wireframe's mock select. Presentation-only: <see cref="ProcessReturnRequest"/>
    /// has no refund-type field, so this is not persisted — flagged as a deviation in apply-progress notes.
    /// </summary>
    public static IReadOnlyList<string> RefundTypeOptions { get; } = ["Efectivo", "Nota crédito", "Cambio de producto"];

    private readonly QuerySalesHistoryUseCase _querySalesHistory;
    private readonly ReprintReceiptUseCase _reprintReceipt;
    private readonly ProcessReturnUseCase _processReturn;
    private readonly GetInvoiceDetailUseCase? _getInvoiceDetail;
    private readonly SendDocumentUseCase? _sendDocument;
    private readonly IPdfService? _pdfService;
    private readonly CancelSaleUseCase? _cancelSale;
    private readonly List<InvoiceSummaryRow> _allInvoices = [];

    private InvoiceSummaryRow? _selectedInvoice;
    private InvoiceDetailResult? _selectedInvoiceDetail;
    private bool _isInvoiceDetailLoading;
    private string _searchText = string.Empty;
    private string _statusFilter = string.Empty;
    private string _cashierFilter = string.Empty;
    private string _returnReason = string.Empty;
    private string _returnDescription = string.Empty;
    private string _refundType = "Efectivo";
    private string _recipientContact = string.Empty;
    private string _message = "Busca facturas por fecha, cliente o número.";
    private int _currentPage = 1;
    private bool _hasMorePages;
    private readonly ICurrentOperatorSession _session;

    public event EventHandler<(string FileName, byte[] Data)>? FileExportRequested;

    public InvoicesViewModel(
        QuerySalesHistoryUseCase querySalesHistory,
        ReprintReceiptUseCase reprintReceipt,
        ProcessReturnUseCase processReturn,
        ICurrentOperatorSession session,
        SendDocumentUseCase? sendDocument = null,
        IPdfService? pdfService = null,
        CancelSaleUseCase? cancelSale = null,
        GetInvoiceDetailUseCase? getInvoiceDetail = null)
    {
        _querySalesHistory = querySalesHistory;
        _reprintReceipt = reprintReceipt;
        _processReturn = processReturn;
        _session = session;
        _getInvoiceDetail = getInvoiceDetail;
        _sendDocument = sendDocument;
        _pdfService = pdfService;
        _cancelSale = cancelSale;

        LoadCommand = new RelayCommand(_ => RunSafe(LoadFirstPageAsync(), ex => Message = $"Error al cargar facturas: {ex.Message}"));
        ReprintCommand = new RelayCommand(_ => RunSafe(ReprintAsync(), ex => Message = $"Error al reimprimir factura: {ex.Message}"), _ => SelectedInvoice is not null && (SelectedInvoice.Status == SaleStatus.Completed || SelectedInvoice.Status == SaleStatus.Cancelled || SelectedInvoice.Status == SaleStatus.Returned));
        ExportPdfCommand = new RelayCommand(_ => RunSafe(ExportPdfAsync(), ex => Message = $"Error al generar el PDF: {ex.Message}"), _ => SelectedInvoice is not null && _pdfService is not null);
        ProcessReturnCommand = new RelayCommand(_ => RunSafe(ProcessReturnAsync(), ex => Message = $"Error al procesar la devolución: {ex.Message}"), _ => SelectedInvoice is not null && SelectedInvoice.Status == SaleStatus.Completed && !string.IsNullOrWhiteSpace(ReturnReason));
        CancelSaleCommand = new RelayCommand(_ => RunSafe(CancelSaleAsync(), ex => Message = $"Error al anular la factura: {ex.Message}"), _ => CanCancelSelectedInvoice());
        SendByEmailCommand = new RelayCommand(_ => RunSafe(SendByEmailAsync(), ex => Message = $"Error al enviar la factura: {ex.Message}"));
        SendByWhatsAppCommand = new RelayCommand(_ => RunSafe(SendByWhatsAppAsync(), ex => Message = $"Error al enviar la factura: {ex.Message}"));
        SelectInvoiceCommand = new RelayCommand(p => SelectedInvoice = p as InvoiceSummaryRow);
        NextPageCommand = new RelayCommand(_ => RunSafe(NextPageAsync(), ex => Message = $"Error al cambiar de página: {ex.Message}"), _ => HasMorePages);
        PreviousPageCommand = new RelayCommand(_ => RunSafe(PreviousPageAsync(), ex => Message = $"Error al cambiar de página: {ex.Message}"), _ => CurrentPage > 1);
    }

    /// <summary>Filtered, presentation-ready rows bound to the invoice grid.</summary>
    public ObservableCollection<InvoiceSummaryRow> FilteredInvoices { get; } = [];

    /// <summary>Distinct cashier/worker names in the loaded page (plus a leading "" = all) for the worker filter dropdown.</summary>
    public ObservableCollection<string> AvailableCashiers { get; } = [];

    public ICommand LoadCommand { get; }

    public ICommand ReprintCommand { get; }

    public ICommand ExportPdfCommand { get; }

    public ICommand ProcessReturnCommand { get; }

    public ICommand CancelSaleCommand { get; }

    public ICommand SendByEmailCommand { get; }

    public ICommand SendByWhatsAppCommand { get; }

    public ICommand SelectInvoiceCommand { get; }

    public ICommand NextPageCommand { get; }

    public ICommand PreviousPageCommand { get; }

    public InvoiceSummaryRow? SelectedInvoice
    {
        get => _selectedInvoice;
        set
        {
            if (SetProperty(ref _selectedInvoice, value))
            {
                ReturnReason = string.Empty;
                ReturnDescription = string.Empty;
                RefundType = "Efectivo";
                RecipientContact = string.Empty;
                SelectedInvoiceDetail = null;
                OnPropertyChanged(nameof(HasSelectedInvoice));
                RaiseSelectionDependentCommands();

                if (value is not null && _getInvoiceDetail is not null)
                {
                    RunSafe(LoadSelectedInvoiceDetailAsync(value.SaleId), ex => Message = $"Error al cargar detalle de factura: {ex.Message}");
                }
            }
        }
    }

    public bool HasSelectedInvoice => SelectedInvoice is not null;

    public InvoiceDetailResult? SelectedInvoiceDetail
    {
        get => _selectedInvoiceDetail;
        private set
        {
            if (SetProperty(ref _selectedInvoiceDetail, value))
            {
                OnPropertyChanged(nameof(HasSelectedInvoiceDetail));
                OnPropertyChanged(nameof(HasDiscounts));
                OnPropertyChanged(nameof(HasTotalItemDiscounts));
                OnPropertyChanged(nameof(HasLoyaltyDiscount));
                OnPropertyChanged(nameof(SubtotalBeforeDiscounts));
                OnPropertyChanged(nameof(TotalItemDiscounts));
                OnPropertyChanged(nameof(LoyaltyDiscount));
                OnPropertyChanged(nameof(TotalDiscounts));
                OnPropertyChanged(nameof(NetTotal));
                OnPropertyChanged(nameof(HasReceivable));
                OnPropertyChanged(nameof(ReceivableOutstandingBalance));
            }
        }
    }

    public bool HasSelectedInvoiceDetail => SelectedInvoiceDetail is not null;

    public bool HasDiscounts => TotalDiscounts > 0;
    public bool HasTotalItemDiscounts => TotalItemDiscounts > 0;
    public bool HasLoyaltyDiscount => LoyaltyDiscount > 0;

    public decimal SubtotalBeforeDiscounts => SelectedInvoiceDetail?.Items.Sum(i => i.Subtotal + i.Discount) ?? 0m;
    public decimal TotalItemDiscounts => SelectedInvoiceDetail?.Items.Sum(i => i.Discount) ?? 0m;
    public decimal LoyaltyDiscount => SelectedInvoiceDetail?.Payments.Where(p => p.Method == PaymentMethod.LoyaltyPoints).Sum(p => p.Amount) ?? 0m;
    public decimal TotalDiscounts => TotalItemDiscounts + LoyaltyDiscount;
    public decimal NetTotal => SelectedInvoiceDetail is null ? 0m : SelectedInvoiceDetail.Total - LoyaltyDiscount;

    public bool HasReceivable =>
        SelectedInvoiceDetail?.OutstandingBalance > 0m ||
        (SelectedInvoiceDetail?.Settlements?.Count > 0);

    public decimal ReceivableOutstandingBalance => SelectedInvoiceDetail?.OutstandingBalance ?? 0m;

    public bool IsInvoiceDetailLoading
    {
        get => _isInvoiceDetailLoading;
        private set => SetProperty(ref _isInvoiceDetailLoading, value);
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value ?? string.Empty);
    }

    /// <summary>
    /// Status filter key ("", "activa", "anulada", "devuelta"). Applied client-side over the already-loaded page
    /// because <see cref="SalesHistoryRequest"/> has no status parameter — presentation-only filtering, no new
    /// backend query.
    /// </summary>
    public string StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (SetProperty(ref _statusFilter, value ?? string.Empty))
            {
                ApplyFilters();
            }
        }
    }

    /// <summary>
    /// Worker/cashier filter key (cashier display name, "" = all). Applied client-side over the already-loaded
    /// page, same as <see cref="StatusFilter"/>. Options come from <see cref="AvailableCashiers"/>.
    /// </summary>
    public string CashierFilter
    {
        get => _cashierFilter;
        set
        {
            if (SetProperty(ref _cashierFilter, value ?? string.Empty))
            {
                ApplyFilters();
            }
        }
    }

    public string ReturnReason
    {
        get => _returnReason;
        set
        {
            if (SetProperty(ref _returnReason, value ?? string.Empty))
            {
                if (ProcessReturnCommand is RelayCommand command)
                {
                    command.RaiseCanExecuteChanged();
                }

                if (CancelSaleCommand is RelayCommand cancelCommand)
                {
                    cancelCommand.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public string ReturnDescription
    {
        get => _returnDescription;
        set => SetProperty(ref _returnDescription, value ?? string.Empty);
    }

    /// <summary>Presentation-only; not persisted (see <see cref="RefundTypeOptions"/> remarks).</summary>
    public string RefundType
    {
        get => _refundType;
        set => SetProperty(ref _refundType, value ?? "Efectivo");
    }

    /// <summary>
    /// Destination email or phone number for the Email/WhatsApp send actions. The wireframe's mock sends
    /// without any destination input; the real <see cref="SendDocumentUseCase"/> requires a recipient contact
    /// and <see cref="Application.DTOs.Invoices.SaleHistoryResult"/> carries no stored customer email/phone,
    /// so this field lets the operator type one — a deliberate, documented deviation from the literal mock.
    /// </summary>
    public string RecipientContact
    {
        get => _recipientContact;
        set => SetProperty(ref _recipientContact, value ?? string.Empty);
    }

    public string Message
    {
        get => _message;
        private set
        {
            if (SetProperty(ref _message, value))
            {
                OnPropertyChanged(nameof(MessageType));
            }
        }
    }

    public string MessageType
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Message) ||
                Message == "Busca facturas por fecha, cliente o número." ||
                Message.StartsWith("Mostrando", StringComparison.OrdinalIgnoreCase))
            {
                return "Info";
            }

            bool containsErrorKeywords = Message.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                                         Message.Contains("no está disponible", StringComparison.OrdinalIgnoreCase) ||
                                         Message.Contains("No encontramos", StringComparison.OrdinalIgnoreCase) ||
                                         Message.Contains("no se pudo", StringComparison.OrdinalIgnoreCase) ||
                                         Message.Contains("No tienes permiso", StringComparison.OrdinalIgnoreCase) ||
                                         Message.StartsWith("Ingresa", StringComparison.OrdinalIgnoreCase) ||
                                         Message.StartsWith("Selecciona", StringComparison.OrdinalIgnoreCase);

            if (containsErrorKeywords)
            {
                return "Error";
            }

            return "Success";
        }
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value < 1 ? 1 : value))
            {
                if (PreviousPageCommand is RelayCommand previous)
                {
                    previous.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public bool HasMorePages
    {
        get => _hasMorePages;
        private set
        {
            if (SetProperty(ref _hasMorePages, value) && NextPageCommand is RelayCommand next)
            {
                next.RaiseCanExecuteChanged();
            }
        }
    }

    public Guid OperatorUserId { get; set; }

    public bool CanCancel => _session.HasPermission(PermissionKeys.Invoices.Cancel);

    public Task LoadFirstPageAsync(CancellationToken cancellationToken = default)
    {
        CurrentPage = 1;
        return LoadAsync(cancellationToken);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new SalesHistoryRequest(
                From: null,
                To: null,
                string.IsNullOrWhiteSpace(SearchText) ? null : SearchText,
                CurrentPage,
                PageSize);

            IReadOnlyList<SaleHistoryResult> results = await _querySalesHistory.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

            _allInvoices.Clear();
            _allInvoices.AddRange(results.Select(InvoiceSummaryRow.FromSale));
            HasMorePages = results.Count == PageSize;
            RebuildAvailableCashiers();
            ApplyFilters();

            Message = FilteredInvoices.Count == 0
                ? "No encontramos facturas con ese criterio."
                : $"Mostrando {FilteredInvoices.Count} factura(s). Selecciona una para ver acciones disponibles.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Message = $"Error al cargar facturas: {ex.Message}";
        }
    }

    public async Task NextPageAsync(CancellationToken cancellationToken = default)
    {
        if (!HasMorePages)
        {
            return;
        }

        CurrentPage += 1;
        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task PreviousPageAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentPage <= 1)
        {
            return;
        }

        CurrentPage -= 1;
        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    public async Task OpenInvoiceAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        if (_getInvoiceDetail is null)
        {
            Message = "El detalle de factura no está disponible en este entorno.";
            return;
        }

        InvoiceDetailResult? detail = await _getInvoiceDetail.ExecuteAsync(saleId, cancellationToken).ConfigureAwait(true);
        if (detail is null)
        {
            Message = "No encontramos la factura solicitada.";
            return;
        }

        SearchText = detail.InvoiceNumber;
        CurrentPage = 1;
        await LoadAsync(cancellationToken).ConfigureAwait(true);

        InvoiceSummaryRow? row = FilteredInvoices.FirstOrDefault(invoice => invoice.SaleId == saleId);
        if (row is null)
        {
            row = InvoiceSummaryRow.FromDetail(detail);
            _allInvoices.Insert(0, row);
            ApplyFilters();
        }

        SelectedInvoice = row;
        SelectedInvoiceDetail = detail;
        Message = $"Factura {detail.InvoiceNumber} abierta desde Clientes.";
    }

    public async Task ReprintAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedInvoice is null)
        {
            Message = "Selecciona una factura";
            return;
        }

        try
        {
            await _reprintReceipt.ExecuteAsync(SelectedInvoice.SaleId, cancellationToken).ConfigureAwait(true);
            Message = $"Factura {SelectedInvoice.InvoiceNumber} enviada a la impresora.";
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }

    public async Task ExportPdfAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedInvoice is null)
        {
            Message = "Selecciona una factura";
            return;
        }

        if (_pdfService is null)
        {
            Message = "La exportación PDF no está disponible en este entorno.";
            return;
        }

        try
        {
            byte[] pdfBytes = await _pdfService.GenerateInvoicePdfAsync(SelectedInvoice.SaleId, cancellationToken).ConfigureAwait(true);
            string fileName = $"Factura_{SanitizeFileName(SelectedInvoice.InvoiceNumber)}.pdf";
            FileExportRequested?.Invoke(this, (fileName, pdfBytes));
            Message = $"PDF de {SelectedInvoice.InvoiceNumber} listo para guardar.";
        }
        catch (Exception ex)
        {
            Message = $"Error al generar el PDF: {ex.Message}";
        }
    }

    public async Task SendByEmailAsync(CancellationToken cancellationToken = default)
        => await SendAsync(SendChannel.Email, cancellationToken).ConfigureAwait(true);

    public async Task SendByWhatsAppAsync(CancellationToken cancellationToken = default)
        => await SendAsync(SendChannel.WhatsApp, cancellationToken).ConfigureAwait(true);

    private async Task SendAsync(SendChannel channel, CancellationToken cancellationToken)
    {
        if (SelectedInvoice is null)
        {
            Message = "Selecciona una factura";
            return;
        }

        if (_sendDocument is null)
        {
            Message = "El envío de documentos no está disponible en este entorno.";
            return;
        }

        if (string.IsNullOrWhiteSpace(RecipientContact))
        {
            Message = "Ingresa el destino del envío.";
            return;
        }

        try
        {
            var request = new SendDocumentRequest(DocumentType.Invoice, SelectedInvoice.SaleId, channel, RecipientContact);
            SendDocumentResult result = await _sendDocument.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);

            Message = result.Success
                ? channel == SendChannel.Email ? "Factura enviada por email" : "Factura enviada por WhatsApp"
                : $"No se pudo enviar la factura: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            Message = $"Error al enviar la factura: {ex.Message}";
        }
    }

    public async Task ProcessReturnAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedInvoice is null)
        {
            Message = "Selecciona una factura";
            return;
        }

        if (string.IsNullOrWhiteSpace(ReturnReason))
        {
            Message = "Selecciona un motivo";
            return;
        }

        try
        {
            string reason = string.IsNullOrWhiteSpace(ReturnDescription)
                ? ReturnReason
                : $"{ReturnReason}: {ReturnDescription}";

            var request = new ProcessReturnRequest(SelectedInvoice.SaleId, OperatorUserId, reason);

            await _processReturn.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
            Message = $"Devolución de {SelectedInvoice.InvoiceNumber} registrada. El stock ha sido restaurado.";

            ReturnReason = string.Empty;
            ReturnDescription = string.Empty;
            RefundType = "Efectivo";
            await LoadAsync(cancellationToken).ConfigureAwait(true);
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

    public async Task CancelSaleAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedInvoice is null)
        {
            Message = "Selecciona una factura";
            return;
        }

        if (!CanCancel)
        {
            Message = "No tienes permiso para anular facturas.";
            return;
        }

        if (_cancelSale is null)
        {
            Message = "La anulación de facturas no está disponible en este entorno.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ReturnReason))
        {
            Message = "Selecciona un motivo";
            return;
        }

        try
        {
            string reason = string.IsNullOrWhiteSpace(ReturnDescription)
                ? ReturnReason
                : $"{ReturnReason}: {ReturnDescription}";

            await _cancelSale.ExecuteAsync(new CancelSaleRequest(SelectedInvoice.SaleId, OperatorUserId, reason), cancellationToken).ConfigureAwait(true);
            string invoiceNumber = SelectedInvoice.InvoiceNumber;

            ReturnReason = string.Empty;
            ReturnDescription = string.Empty;
            RefundType = "Efectivo";
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            Message = $"Factura {invoiceNumber} anulada correctamente. El stock ha sido restaurado.";
        }
        catch (UnauthorizedActionException)
        {
            Message = "No tienes permiso para anular facturas.";
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

    private void ApplyFilters()
    {
        IEnumerable<InvoiceSummaryRow> filtered = _allInvoices;

        if (!string.IsNullOrEmpty(StatusFilter))
        {
            filtered = filtered.Where(row => row.StatusFilterKey == StatusFilter);
        }

        if (!string.IsNullOrEmpty(CashierFilter))
        {
            filtered = filtered.Where(row => row.CashierName == CashierFilter);
        }

        FilteredInvoices.Clear();
        foreach (InvoiceSummaryRow row in filtered)
        {
            FilteredInvoices.Add(row);
        }
    }

    private async Task LoadSelectedInvoiceDetailAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        if (_getInvoiceDetail is null)
        {
            return;
        }

        try
        {
            IsInvoiceDetailLoading = true;
            InvoiceDetailResult? detail = await _getInvoiceDetail.ExecuteAsync(saleId, cancellationToken).ConfigureAwait(true);
            if (SelectedInvoice?.SaleId == saleId)
            {
                SelectedInvoiceDetail = detail;
            }
        }
        finally
        {
            IsInvoiceDetailLoading = false;
        }
    }

    private void RebuildAvailableCashiers()
    {
        string current = CashierFilter;

        AvailableCashiers.Clear();
        AvailableCashiers.Add(string.Empty); // "" = todos
        foreach (string name in _allInvoices.Select(row => row.CashierName).Distinct().OrderBy(name => name, StringComparer.CurrentCulture))
        {
            AvailableCashiers.Add(name);
        }

        // Drop a stale selection so the grid doesn't silently show nothing.
        if (!AvailableCashiers.Contains(current))
        {
            _cashierFilter = string.Empty;
            OnPropertyChanged(nameof(CashierFilter));
        }
    }

    private void RaiseSelectionDependentCommands()
    {
        if (ReprintCommand is RelayCommand reprintCommand)
        {
            reprintCommand.RaiseCanExecuteChanged();
        }

        if (ExportPdfCommand is RelayCommand exportPdfCommand)
        {
            exportPdfCommand.RaiseCanExecuteChanged();
        }

        if (ProcessReturnCommand is RelayCommand returnCommand)
        {
            returnCommand.RaiseCanExecuteChanged();
        }

        if (CancelSaleCommand is RelayCommand cancelCommand)
        {
            cancelCommand.RaiseCanExecuteChanged();
        }
    }

    private bool CanCancelSelectedInvoice() =>
        CanCancel &&
        SelectedInvoice is not null &&
        SelectedInvoice.Status == SaleStatus.Completed &&
        !string.IsNullOrWhiteSpace(ReturnReason);

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Factura";
        }

        char[] invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray();
        string sanitized = new(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "Factura" : sanitized;
    }
}
