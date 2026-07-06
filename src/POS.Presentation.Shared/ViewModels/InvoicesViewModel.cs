using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using POS.Application.DTOs.Communication;
using POS.Application.DTOs.Invoices;
using POS.Application.UseCases.Communication;
using POS.Application.UseCases.Invoices;
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
    private readonly SendDocumentUseCase? _sendDocument;
    private readonly List<InvoiceSummaryRow> _allInvoices = [];

    private InvoiceSummaryRow? _selectedInvoice;
    private string _searchText = string.Empty;
    private string _statusFilter = string.Empty;
    private string _returnReason = string.Empty;
    private string _returnDescription = string.Empty;
    private string _refundType = "Efectivo";
    private string _recipientContact = string.Empty;
    private string _message = "Busca facturas por fecha, cliente o número.";
    private int _currentPage = 1;
    private bool _hasMorePages;

    public InvoicesViewModel(
        QuerySalesHistoryUseCase querySalesHistory,
        ReprintReceiptUseCase reprintReceipt,
        ProcessReturnUseCase processReturn,
        SendDocumentUseCase? sendDocument = null)
    {
        _querySalesHistory = querySalesHistory;
        _reprintReceipt = reprintReceipt;
        _processReturn = processReturn;
        _sendDocument = sendDocument;

        LoadCommand = new RelayCommand(_ => _ = LoadFirstPageAsync());
        ReprintCommand = new RelayCommand(_ => _ = ReprintAsync(), _ => SelectedInvoice is not null && SelectedInvoice.Status == SaleStatus.Completed);
        ProcessReturnCommand = new RelayCommand(_ => _ = ProcessReturnAsync(), _ => SelectedInvoice is not null && SelectedInvoice.Status == SaleStatus.Completed && !string.IsNullOrWhiteSpace(ReturnReason));
        SendByEmailCommand = new RelayCommand(_ => _ = SendByEmailAsync());
        SendByWhatsAppCommand = new RelayCommand(_ => _ = SendByWhatsAppAsync());
        SelectInvoiceCommand = new RelayCommand(p => SelectedInvoice = p as InvoiceSummaryRow);
        NextPageCommand = new RelayCommand(_ => _ = NextPageAsync(), _ => HasMorePages);
        PreviousPageCommand = new RelayCommand(_ => _ = PreviousPageAsync(), _ => CurrentPage > 1);
    }

    /// <summary>Filtered, presentation-ready rows bound to the invoice grid.</summary>
    public ObservableCollection<InvoiceSummaryRow> FilteredInvoices { get; } = [];

    public ICommand LoadCommand { get; }

    public ICommand ReprintCommand { get; }

    public ICommand ProcessReturnCommand { get; }

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
                OnPropertyChanged(nameof(HasSelectedInvoice));
                RaiseSelectionDependentCommands();
            }
        }
    }

    public bool HasSelectedInvoice => SelectedInvoice is not null;

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
                ApplyStatusFilter();
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
        private set => SetProperty(ref _message, value);
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

    public Task LoadFirstPageAsync(CancellationToken cancellationToken = default)
    {
        CurrentPage = 1;
        return LoadAsync(cancellationToken);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
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
        ApplyStatusFilter();

        Message = FilteredInvoices.Count == 0
            ? "No encontramos facturas con ese criterio."
            : $"Mostrando {FilteredInvoices.Count} factura(s). Selecciona una para ver acciones disponibles.";
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

    private void ApplyStatusFilter()
    {
        IEnumerable<InvoiceSummaryRow> filtered = string.IsNullOrEmpty(StatusFilter)
            ? _allInvoices
            : _allInvoices.Where(row => row.StatusFilterKey == StatusFilter);

        FilteredInvoices.Clear();
        foreach (InvoiceSummaryRow row in filtered)
        {
            FilteredInvoices.Add(row);
        }
    }

    private void RaiseSelectionDependentCommands()
    {
        if (ReprintCommand is RelayCommand reprintCommand)
        {
            reprintCommand.RaiseCanExecuteChanged();
        }

        if (ProcessReturnCommand is RelayCommand returnCommand)
        {
            returnCommand.RaiseCanExecuteChanged();
        }
    }
}
