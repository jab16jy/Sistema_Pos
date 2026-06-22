using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Reporting;
using POS.Application.Interfaces;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

/// <summary>
/// ViewModel for the Reports module.
/// Loads operational totals (billed, cash, digital, receivable settlements) from persisted records.
/// All business logic lives in the Application use case; this class only manages UI state.
/// </summary>
public sealed class ReportsViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    private readonly IGetOperationalReportUseCase _reportUseCase;
    private decimal _reportBilledTotal;
    private decimal _reportCashCollected;
    private decimal _reportDigitalCollected;
    private decimal _reportReceivableSettlements;
    private decimal _reportExpectedDrawerCash;
    private bool _isLoading;
    private bool _hasError;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;

    public ReportsViewModel(IGetOperationalReportUseCase reportUseCase)
    {
        _reportUseCase = reportUseCase ?? throw new ArgumentNullException(nameof(reportUseCase));

        LoadReportCommand = new RelayCommand(
            async _ => await LoadReportAsync(
                DateTimeOffset.UtcNow.Date,
                DateTimeOffset.UtcNow.Date.AddDays(1).AddTicks(-1)).ConfigureAwait(true));
    }

    // ── Report totals (read-only output) ──

    public decimal ReportBilledTotal
    {
        get => _reportBilledTotal;
        private set => SetProperty(ref _reportBilledTotal, value);
    }

    public decimal ReportCashCollected
    {
        get => _reportCashCollected;
        private set => SetProperty(ref _reportCashCollected, value);
    }

    public decimal ReportDigitalCollected
    {
        get => _reportDigitalCollected;
        private set => SetProperty(ref _reportDigitalCollected, value);
    }

    public decimal ReportReceivableSettlements
    {
        get => _reportReceivableSettlements;
        private set => SetProperty(ref _reportReceivableSettlements, value);
    }

    public decimal ReportExpectedDrawerCash
    {
        get => _reportExpectedDrawerCash;
        private set => SetProperty(ref _reportExpectedDrawerCash, value);
    }

    // ── State ──

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value ?? string.Empty);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value ?? string.Empty);
    }

    // ── Formatted display helpers ──

    public string BilledTotalText => FormatCurrency(ReportBilledTotal);
    public string CashCollectedText => FormatCurrency(ReportCashCollected);
    public string DigitalCollectedText => FormatCurrency(ReportDigitalCollected);
    public string ReceivableSettlementsText => FormatCurrency(ReportReceivableSettlements);
    public string ExpectedDrawerCashText => FormatCurrency(ReportExpectedDrawerCash);

    // ── Commands ──

    public ICommand LoadReportCommand { get; }

    // ── Load ──

    /// <summary>
    /// Loads the operational report for the given date range.
    /// Validates the range and delegates to the use case.
    /// </summary>
    public async Task LoadReportAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        try
        {
            var request = new ReportRangeRequest(from, to);
            var result = await _reportUseCase.ExecuteAsync(request, ct).ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                HasError = true;
                ErrorMessage = result.Error ?? "Error al cargar el reporte.";
                ClearTotals();
                return;
            }

            var report = result.Report!;
            ReportBilledTotal = report.BilledSalesTotal;
            ReportCashCollected = report.SaleCashCollected;
            ReportDigitalCollected = report.SaleDigitalCollected;
            ReportReceivableSettlements = report.ReceivableSettlementTotal;
            ReportExpectedDrawerCash = report.ExpectedDrawerCash;

            OnPropertyChanged(nameof(BilledTotalText));
            OnPropertyChanged(nameof(CashCollectedText));
            OnPropertyChanged(nameof(DigitalCollectedText));
            OnPropertyChanged(nameof(ReceivableSettlementsText));
            OnPropertyChanged(nameof(ExpectedDrawerCashText));

            if (report.BilledSalesTotal == 0m && report.ReceivableSettlementTotal == 0m)
            {
                StatusMessage = "Sin actividad registrada para el periodo seleccionado.";
            }
            else
            {
                StatusMessage = $"Reporte cargado. Total facturado: {FormatCurrency(report.BilledSalesTotal)}.";
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Error inesperado al cargar el reporte: {ex.Message}";
            ClearTotals();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ClearTotals()
    {
        ReportBilledTotal = 0m;
        ReportCashCollected = 0m;
        ReportDigitalCollected = 0m;
        ReportReceivableSettlements = 0m;
        ReportExpectedDrawerCash = 0m;
    }

    private static string FormatCurrency(decimal amount) =>
        $"${amount.ToString("N0", ColombianCulture)}";
}
