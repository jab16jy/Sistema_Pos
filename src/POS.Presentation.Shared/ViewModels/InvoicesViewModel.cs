using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Invoices;
using POS.Application.UseCases.Invoices;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class InvoicesViewModel : ViewModelBase
{
    private readonly QuerySalesHistoryUseCase _querySalesHistory;
    private readonly ReprintReceiptUseCase _reprintReceipt;
    private readonly ProcessReturnUseCase _processReturn;
    private SaleHistoryResult? _selectedSale;
    private string _searchText = string.Empty;
    private DateOnly? _fromDate;
    private DateOnly? _toDate;
    private string _returnReason = string.Empty;
    private string _message = "Busca ventas por fecha, cliente o número de factura.";

    public InvoicesViewModel(
        QuerySalesHistoryUseCase querySalesHistory,
        ReprintReceiptUseCase reprintReceipt,
        ProcessReturnUseCase processReturn)
    {
        _querySalesHistory = querySalesHistory;
        _reprintReceipt = reprintReceipt;
        _processReturn = processReturn;
        LoadCommand = new RelayCommand(_ => _ = LoadAsync());
        ReprintCommand = new RelayCommand(_ => _ = ReprintAsync(), _ => SelectedSale is not null && SelectedSale.Status == POS.Domain.Enums.SaleStatus.Completed);
        ProcessReturnCommand = new RelayCommand(_ => _ = ProcessReturnAsync(), _ => SelectedSale is not null && SelectedSale.Status == POS.Domain.Enums.SaleStatus.Completed && !string.IsNullOrWhiteSpace(ReturnReason));
    }

    public ObservableCollection<SaleHistoryResult> Sales { get; } = [];

    public ICommand LoadCommand { get; }

    public ICommand ReprintCommand { get; }

    public ICommand ProcessReturnCommand { get; }

    public SaleHistoryResult? SelectedSale
    {
        get => _selectedSale;
        set
        {
            if (SetProperty(ref _selectedSale, value))
            {
                ReturnReason = string.Empty;

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
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public DateOnly? FromDate
    {
        get => _fromDate;
        set => SetProperty(ref _fromDate, value);
    }

    public DateOnly? ToDate
    {
        get => _toDate;
        set => SetProperty(ref _toDate, value);
    }

    public string ReturnReason
    {
        get => _returnReason;
        set
        {
            if (SetProperty(ref _returnReason, value))
            {
                if (ProcessReturnCommand is RelayCommand command)
                {
                    command.RaiseCanExecuteChanged();
                }
            }
        }
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public Guid OperatorUserId { get; set; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var request = new SalesHistoryRequest(
            FromDate,
            ToDate,
            string.IsNullOrWhiteSpace(SearchText) ? null : SearchText);

        var results = await _querySalesHistory.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
        Sales.Clear();
        foreach (var sale in results)
        {
            Sales.Add(sale);
        }

        Message = Sales.Count == 0
            ? "No encontramos ventas con ese criterio."
            : $"Mostrando {Sales.Count} venta(s). Selecciona una para reimprimir o procesar devolución.";
    }

    public async Task ReprintAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSale is null)
        {
            Message = "Selecciona una venta para reimprimir el recibo.";
            return;
        }

        try
        {
            await _reprintReceipt.ExecuteAsync(SelectedSale.SaleId, cancellationToken).ConfigureAwait(true);
            Message = $"Recibo de {SelectedSale.InvoiceNumber} enviado a la impresora.";
        }
        catch (InvalidOperationException ex)
        {
            Message = ex.Message;
        }
    }

    public async Task ProcessReturnAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSale is null)
        {
            Message = "Selecciona una venta para procesar la devolución.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ReturnReason))
        {
            Message = "Debes ingresar una razón para la devolución.";
            return;
        }

        try
        {
            var request = new ProcessReturnRequest(
                SelectedSale.SaleId,
                OperatorUserId,
                ReturnReason);

            await _processReturn.ExecuteAsync(request, cancellationToken).ConfigureAwait(true);
            Message = $"Devolución procesada para {SelectedSale.InvoiceNumber}. El stock ha sido restaurado.";

            ReturnReason = string.Empty;
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
}
