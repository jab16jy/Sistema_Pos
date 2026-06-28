using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Reporting;
using POS.Application.Interfaces;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class ReportsViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    private readonly IGetOperationalReportUseCase _reportUseCase;
    private readonly IProfitQueryService? _profitQueryService;
    private readonly IReportExportService? _exportService;

    // Last-loaded data for export
    private OperationalReportDto? _lastOperationalReport;
    private DateTimeOffset _lastOperationalFrom;
    private DateTimeOffset _lastOperationalTo;
    private DateOnly _lastAnalyticsFrom;
    private DateOnly _lastAnalyticsTo;

    private decimal _reportBilledTotal;
    private decimal _reportCashCollected;
    private decimal _reportDigitalCollected;
    private decimal _reportReceivableSettlements;
    private decimal _reportExpectedDrawerCash;
    private bool _isLoading;
    private bool _hasError;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;
    private PeriodComparisonResult? _periodComparison;

    public event EventHandler<(string FileName, byte[] Data)>? FileExportRequested;

    public ReportsViewModel(IGetOperationalReportUseCase reportUseCase)
        : this(reportUseCase, null, null)
    {
    }

    public ReportsViewModel(IGetOperationalReportUseCase reportUseCase, IProfitQueryService? profitQueryService)
        : this(reportUseCase, profitQueryService, null)
    {
    }

    public ReportsViewModel(IGetOperationalReportUseCase reportUseCase, IProfitQueryService? profitQueryService, IReportExportService? exportService)
    {
        _reportUseCase = reportUseCase ?? throw new ArgumentNullException(nameof(reportUseCase));
        _profitQueryService = profitQueryService;
        _exportService = exportService;

        TopSellers = [];
        PlatformBreakdown = [];
        ProductMargins = [];

        LoadReportCommand = new RelayCommand(
            async _ => await LoadReportAsync(
                DateTimeOffset.UtcNow.Date,
                DateTimeOffset.UtcNow.Date.AddDays(1).AddTicks(-1)).ConfigureAwait(true));

        LoadAnalyticsCommand = new RelayCommand(
            async _ => await LoadAnalyticsAsync(
                DateOnly.FromDateTime(DateTime.Today),
                DateOnly.FromDateTime(DateTime.Today)).ConfigureAwait(true));

        ExportOperationalPdfCommand = new RelayCommand(
            async _ => await ExportOperationalAsync(pdf: true).ConfigureAwait(true),
            _ => _exportService is not null && _lastOperationalReport is not null);

        ExportOperationalExcelCommand = new RelayCommand(
            async _ => await ExportOperationalAsync(pdf: false).ConfigureAwait(true),
            _ => _exportService is not null && _lastOperationalReport is not null);

        ExportProfitPdfCommand = new RelayCommand(
            async _ => await ExportProfitAsync(pdf: true).ConfigureAwait(true),
            _ => _exportService is not null && _periodComparison is not null);

        ExportProfitExcelCommand = new RelayCommand(
            async _ => await ExportProfitAsync(pdf: false).ConfigureAwait(true),
            _ => _exportService is not null && _periodComparison is not null);
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
    public ICommand LoadAnalyticsCommand { get; }
    public ICommand ExportOperationalPdfCommand { get; }
    public ICommand ExportOperationalExcelCommand { get; }
    public ICommand ExportProfitPdfCommand { get; }
    public ICommand ExportProfitExcelCommand { get; }

    // ── Analytics collections ──

    public ObservableCollection<TopSellerResult> TopSellers { get; }

    public PeriodComparisonResult? PeriodComparison
    {
        get => _periodComparison;
        private set => SetProperty(ref _periodComparison, value);
    }

    public ObservableCollection<PlatformBreakdownResult> PlatformBreakdown { get; }

    public ObservableCollection<ProductMarginResult> ProductMargins { get; }

    // ── Load ──

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

            _lastOperationalReport = report;
            _lastOperationalFrom = from;
            _lastOperationalTo = to;

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

    /// <summary>
    /// Loads profit analytics: top sellers, period comparison, platform breakdown, and product margins.
    /// </summary>
    public async Task LoadAnalyticsAsync(
        DateOnly currentFrom,
        DateOnly currentTo,
        CancellationToken ct = default)
    {
        if (_profitQueryService is null)
        {
            StatusMessage = "El servicio de analisis de rentabilidad no esta disponible.";
            return;
        }

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            // Compute prior period (same length, immediately before current)
            int days = currentTo.DayNumber - currentFrom.DayNumber + 1;
            var priorTo = currentFrom.AddDays(-1);
            var priorFrom = priorTo.AddDays(-(days - 1));

            var topSellersTask = _profitQueryService.GetTopSellersAsync(currentFrom, currentTo, 10, ct);
            var comparisonTask = _profitQueryService.GetPeriodComparisonAsync(currentFrom, currentTo, priorFrom, priorTo, ct);
            var platformTask = _profitQueryService.GetPlatformBreakdownAsync(currentFrom, currentTo, ct);
            var marginsTask = _profitQueryService.GetMarginByProductAsync(currentFrom, currentTo, ct);

            await Task.WhenAll(topSellersTask, comparisonTask, platformTask, marginsTask).ConfigureAwait(true);

            TopSellers.Clear();
            foreach (var item in topSellersTask.Result)
            {
                TopSellers.Add(item);
            }

            PeriodComparison = comparisonTask.Result;

            PlatformBreakdown.Clear();
            foreach (var item in platformTask.Result)
            {
                PlatformBreakdown.Add(item);
            }

            ProductMargins.Clear();
            foreach (var item in marginsTask.Result)
            {
                ProductMargins.Add(item);
            }

            _lastAnalyticsFrom = currentFrom;
            _lastAnalyticsTo = currentTo;

            StatusMessage = "Analisis de rentabilidad cargado exitosamente.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Error al cargar analisis: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExportOperationalAsync(bool pdf)
    {
        if (_exportService is null || _lastOperationalReport is null) return;
        try
        {
            IsLoading = true;
            byte[] data = pdf
                ? await _exportService.ExportOperationalReportPdfAsync(_lastOperationalReport, _lastOperationalFrom, _lastOperationalTo).ConfigureAwait(true)
                : await _exportService.ExportOperationalReportExcelAsync(_lastOperationalReport, _lastOperationalFrom, _lastOperationalTo).ConfigureAwait(true);

            string ext = pdf ? "pdf" : "xlsx";
            string name = $"Reporte_Operacional_{_lastOperationalFrom.ToLocalTime():yyyyMMdd}.{ext}";
            FileExportRequested?.Invoke(this, (name, data));
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Error al exportar: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ExportProfitAsync(bool pdf)
    {
        if (_exportService is null || _periodComparison is null) return;
        try
        {
            IsLoading = true;
            var topSellers = (IReadOnlyList<TopSellerResult>)TopSellers;
            var margins = (IReadOnlyList<ProductMarginResult>)ProductMargins;

            byte[] data = pdf
                ? await _exportService.ExportProfitReportPdfAsync(topSellers, _periodComparison, margins, _lastAnalyticsFrom, _lastAnalyticsTo).ConfigureAwait(true)
                : await _exportService.ExportProfitReportExcelAsync(topSellers, _periodComparison, margins, _lastAnalyticsFrom, _lastAnalyticsTo).ConfigureAwait(true);

            string ext = pdf ? "pdf" : "xlsx";
            string name = $"Reporte_Rentabilidad_{_lastAnalyticsFrom:yyyyMMdd}.{ext}";
            FileExportRequested?.Invoke(this, (name, data));
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Error al exportar: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string FormatCurrency(decimal amount) =>
        $"${amount.ToString("N0", ColombianCulture)}";
}
