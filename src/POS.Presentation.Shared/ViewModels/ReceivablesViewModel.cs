using System.Collections.ObjectModel;
using System.Windows.Input;
using POS.Application.DTOs.Receivables;
using POS.Application.Interfaces;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

/// <summary>
/// ViewModel for the Receivables module: displays open customer balances
/// and allows applying partial or full payments.
/// </summary>
public sealed class ReceivablesViewModel : ViewModelBase
{
    private readonly IReceivableQueryService _queryService;
    private readonly IApplyReceivablePaymentUseCase? _applyPaymentUseCase;
    private ReceivableSummaryRow? _selectedReceivable;
    private decimal _paymentAmount;
    private bool _isLoading;
    private string _statusMessage = string.Empty;

    public ReceivablesViewModel(IReceivableQueryService queryService)
        : this(queryService, null)
    {
    }

    public ReceivablesViewModel(IReceivableQueryService queryService, IApplyReceivablePaymentUseCase? applyPaymentUseCase)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _applyPaymentUseCase = applyPaymentUseCase;
        Receivables = [];

        LoadCommand = new RelayCommand(async _ => await LoadAsync().ConfigureAwait(true));
        ApplyPaymentCommand = new RelayCommand(async _ => await ApplyPaymentAsync().ConfigureAwait(true));
    }

    public ObservableCollection<ReceivableSummaryRow> Receivables { get; }

    public ICommand LoadCommand { get; }
    public ICommand ApplyPaymentCommand { get; }

    public ReceivableSummaryRow? SelectedReceivable
    {
        get => _selectedReceivable;
        set => SetProperty(ref _selectedReceivable, value);
    }

    /// <summary>Amount to apply as a payment toward the selected receivable.</summary>
    public decimal PaymentAmount
    {
        get => _paymentAmount;
        set
        {
            decimal normalized = value < 0m ? 0m : decimal.Round(value, 2, MidpointRounding.AwayFromZero);
            SetProperty(ref _paymentAmount, normalized);
        }
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

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StatusMessage = string.Empty;

        try
        {
            IReadOnlyList<ReceivableSummary> summaries =
                await _queryService.GetOpenReceivablesAsync(null, 1, 50, cancellationToken).ConfigureAwait(true);

            Receivables.Clear();
            foreach (ReceivableSummary s in summaries)
            {
                Receivables.Add(ReceivableSummaryRow.FromSummary(s));
            }

            if (Receivables.Count == 0)
            {
                StatusMessage = "Sin saldos pendientes por cobrar en este momento.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error al cargar cuentas por cobrar: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Applies the entered PaymentAmount toward the selected receivable.
    /// Validates that a row is selected, amount > 0, and amount does not exceed balance.
    /// </summary>
    public async Task ApplyPaymentAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedReceivable is null)
        {
            StatusMessage = "Selecciona un saldo pendiente antes de registrar el abono.";
            return;
        }

        if (PaymentAmount <= 0m)
        {
            StatusMessage = "El monto del abono debe ser mayor a cero.";
            return;
        }

        if (PaymentAmount > SelectedReceivable.OutstandingBalance)
        {
            StatusMessage = $"El abono (${PaymentAmount:N0}) excede el saldo pendiente (${SelectedReceivable.OutstandingBalance:N0}).";
            return;
        }

        if (_applyPaymentUseCase is null)
        {
            StatusMessage = "Módulo de abonos no disponible en este entorno.";
            return;
        }

        try
        {
            decimal appliedAmount = PaymentAmount;
            await _applyPaymentUseCase.ExecuteAsync(SelectedReceivable.ReceivableId, PaymentAmount, cancellationToken)
                .ConfigureAwait(true);

            PaymentAmount = 0m;
            await LoadAsync(cancellationToken).ConfigureAwait(true);
            // Set success message after reload so it remains visible to the user
            StatusMessage = $"Abono de ${appliedAmount:N0} registrado correctamente.";
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }
}

/// <summary>Row model for displaying a receivable summary in the UI.</summary>
public sealed class ReceivableSummaryRow
{
    private ReceivableSummaryRow()
    {
    }

    public Guid ReceivableId { get; private init; }
    public Guid CustomerId { get; private init; }
    public string CustomerName { get; private init; } = string.Empty;
    public Guid SaleId { get; private init; }
    public string InvoiceNumber { get; private init; } = string.Empty;
    public decimal OriginalBalance { get; private init; }
    public decimal OutstandingBalance { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    public static ReceivableSummaryRow FromSummary(ReceivableSummary s) => new()
    {
        ReceivableId = s.ReceivableId,
        CustomerId = s.CustomerId,
        CustomerName = s.CustomerName,
        SaleId = s.SaleId,
        InvoiceNumber = s.InvoiceNumber,
        OriginalBalance = s.OriginalBalance,
        OutstandingBalance = s.OutstandingBalance,
        CreatedAt = s.CreatedAt
    };
}
