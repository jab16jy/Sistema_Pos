using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Reporting;
using POS.Application.Interfaces;
using POS.Application.UseCases.Reporting;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class ReportsViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    private readonly IGetOperationalReportUseCase _reportUseCase;
    private readonly IProfitQueryService? _profitQueryService;
    private readonly IReportExportService? _exportService;
    private readonly CloseCashRegisterUseCase? _closeCashUseCase;
    private readonly ICurrentOperatorSession? _session;

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

    // Date range fields
    private DateTime _reportFromDate = DateTime.Today;
    private DateTime _reportToDate = DateTime.Today;
    private DateOnly _analyticsFromDate = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateOnly _analyticsToDate = DateOnly.FromDateTime(DateTime.Today);

    // Cash closing fields
    private string _countedCashText = string.Empty;
    private string _closingStatusMessage = string.Empty;

    public event EventHandler<(string FileName, byte[] Data)>? FileExportRequested;

    public ReportsViewModel(IGetOperationalReportUseCase reportUseCase)
        : this(reportUseCase, null, null, null, null)
    {
    }

    public ReportsViewModel(IGetOperationalReportUseCase reportUseCase, IProfitQueryService? profitQueryService)
        : this(reportUseCase, profitQueryService, null, null, null)
    {
    }

    public ReportsViewModel(IGetOperationalReportUseCase reportUseCase, IProfitQueryService? profitQueryService, IReportExportService? exportService)
        : this(reportUseCase, profitQueryService, exportService, null, null)
    {
    }

    public ReportsViewModel(
        IGetOperationalReportUseCase reportUseCase,
        IProfitQueryService? profitQueryService,
        IReportExportService? exportService,
        CloseCashRegisterUseCase? closeCashUseCase,
        ICurrentOperatorSession? session)
    {
        _reportUseCase = reportUseCase ?? throw new ArgumentNullException(nameof(reportUseCase));
        _profitQueryService = profitQueryService;
        _exportService = exportService;
        _closeCashUseCase = closeCashUseCase;
        _session = session;

        TopSellers = [];
        PlatformBreakdown = [];
        ProductMargins = [];

        LoadReportCommand = new RelayCommand(
            async _ => await LoadReportAsync(
                new DateTimeOffset(_reportFromDate, TimeZoneInfo.Local.GetUtcOffset(_reportFromDate)),
                new DateTimeOffset(_reportToDate, TimeZoneInfo.Local.GetUtcOffset(_reportToDate)).AddDays(1).AddTicks(-1))
            .ConfigureAwait(true));

        LoadAnalyticsCommand = new RelayCommand(
            async _ => await LoadAnalyticsAsync(_analyticsFromDate, _analyticsToDate).ConfigureAwait(true));

        SetTodayCommand = new RelayCommand(_ =>
        {
            ReportFromDate = DateTime.Today;
            ReportToDate = DateTime.Today;
        });

        SetThisWeekCommand = new RelayCommand(_ =>
        {
            var today = DateTime.Today;
            int daysFromMonday = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            ReportFromDate = today.AddDays(-daysFromMonday);
            ReportToDate = today;
        });

        SetThisMonthCommand = new RelayCommand(_ =>
        {
            var today = DateTime.Today;
            ReportFromDate = new DateTime(today.Year, today.Month, 1);
            ReportToDate = today;
        });

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

        CloseCashCommand = new RelayCommand(
            async _ => await CloseCashAsync().ConfigureAwait(true),
            _ => CanCloseCash());
    }

    // ── Date range properties ──

    public DateTime ReportFromDate
    {
        get => _reportFromDate;
        set => SetProperty(ref _reportFromDate, value);
    }

    public DateTime ReportToDate
    {
        get => _reportToDate;
        set => SetProperty(ref _reportToDate, value);
    }

    public DateOnly AnalyticsFromDate
    {
        get => _analyticsFromDate;
        set
        {
            if (SetProperty(ref _analyticsFromDate, value))
            {
                OnPropertyChanged(nameof(AnalyticsFromDateAsDateTime));
            }
        }
    }

    public DateOnly AnalyticsToDate
    {
        get => _analyticsToDate;
        set
        {
            if (SetProperty(ref _analyticsToDate, value))
            {
                OnPropertyChanged(nameof(AnalyticsToDateAsDateTime));
            }
        }
    }

    public DateTime? AnalyticsFromDateAsDateTime
    {
        get => _analyticsFromDate.ToDateTime(TimeOnly.MinValue);
        set
        {
            if (value.HasValue)
                AnalyticsFromDate = DateOnly.FromDateTime(value.Value);
        }
    }

    public DateTime? AnalyticsToDateAsDateTime
    {
        get => _analyticsToDate.ToDateTime(TimeOnly.MinValue);
        set
        {
            if (value.HasValue)
                AnalyticsToDate = DateOnly.FromDateTime(value.Value);
        }
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
        private set
        {
            if (SetProperty(ref _reportExpectedDrawerCash, value))
            {
                OnPropertyChanged(nameof(ExpectedCashText));
                OnPropertyChanged(nameof(DifferenceText));
                OnPropertyChanged(nameof(DifferenceIsNegative));
            }
        }
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

    // ── Cash closing ──

    public string CountedCashText
    {
        get => _countedCashText;
        set
        {
            if (SetProperty(ref _countedCashText, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(DifferenceText));
                OnPropertyChanged(nameof(DifferenceIsNegative));
                OnPropertyChanged(nameof(ExpectedCashText));
            }
        }
    }

    public string ExpectedCashText => _lastOperationalReport is not null
        ? FormatCurrency(ReportExpectedDrawerCash)
        : "—";

    public string DifferenceText
    {
        get
        {
            if (_lastOperationalReport is null) return "—";
            if (!decimal.TryParse(_countedCashText, NumberStyles.Any, ColombianCulture, out decimal counted) &&
                !decimal.TryParse(_countedCashText, NumberStyles.Any, CultureInfo.InvariantCulture, out counted))
                return "—";
            return FormatCurrency(counted - ReportExpectedDrawerCash);
        }
    }

    public bool DifferenceIsNegative
    {
        get
        {
            if (_lastOperationalReport is null) return false;
            if (!decimal.TryParse(_countedCashText, NumberStyles.Any, ColombianCulture, out decimal counted) &&
                !decimal.TryParse(_countedCashText, NumberStyles.Any, CultureInfo.InvariantCulture, out counted))
                return false;
            return counted < ReportExpectedDrawerCash;
        }
    }

    public string ClosingStatusMessage
    {
        get => _closingStatusMessage;
        private set
        {
            if (SetProperty(ref _closingStatusMessage, value ?? string.Empty))
                OnPropertyChanged(nameof(HasClosingStatus));
        }
    }

    public bool HasClosingStatus => !string.IsNullOrEmpty(_closingStatusMessage);

    // ── Commands ──

    public ICommand LoadReportCommand { get; }
    public ICommand LoadAnalyticsCommand { get; }
    public ICommand SetTodayCommand { get; }
    public ICommand SetThisWeekCommand { get; }
    public ICommand SetThisMonthCommand { get; }
    public ICommand ExportOperationalPdfCommand { get; }
    public ICommand ExportOperationalExcelCommand { get; }
    public ICommand ExportProfitPdfCommand { get; }
    public ICommand ExportProfitExcelCommand { get; }
    public ICommand CloseCashCommand { get; }

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
        ClosingStatusMessage = string.Empty;

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
            OnPropertyChanged(nameof(ExpectedCashText));

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
        _lastOperationalReport = null;
    }

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
                TopSellers.Add(item);

            PeriodComparison = comparisonTask.Result;

            PlatformBreakdown.Clear();
            foreach (var item in platformTask.Result)
                PlatformBreakdown.Add(item);

            ProductMargins.Clear();
            foreach (var item in marginsTask.Result)
                ProductMargins.Add(item);

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

    private bool CanCloseCash()
    {
        if (_closeCashUseCase is null || _lastOperationalReport is null) return false;
        if (decimal.TryParse(_countedCashText, NumberStyles.Any, ColombianCulture, out decimal v1)) return v1 >= 0;
        if (decimal.TryParse(_countedCashText, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v2)) return v2 >= 0;
        return false;
    }

    private async Task CloseCashAsync()
    {
        if (_closeCashUseCase is null || _lastOperationalReport is null) return;

        if (!decimal.TryParse(_countedCashText, NumberStyles.Any, ColombianCulture, out decimal counted) &&
            !decimal.TryParse(_countedCashText, NumberStyles.Any, CultureInfo.InvariantCulture, out counted))
        {
            ClosingStatusMessage = "Ingresa un monto de efectivo contado válido.";
            return;
        }

        IsLoading = true;
        ClosingStatusMessage = string.Empty;

        try
        {
            var operatorId = _session?.OperatorId ?? Guid.Empty;
            var request = new CashClosingRequest(operatorId, _lastOperationalFrom, _lastOperationalTo, counted);
            var result = await _closeCashUseCase.ExecuteAsync(request).ConfigureAwait(true);

            if (result.IsSuccess && result.Summary is not null)
            {
                var diff = result.Summary.Difference;
                string diffText = diff >= 0
                    ? $"Sobrante: {FormatCurrency(diff)}"
                    : $"Faltante: {FormatCurrency(Math.Abs(diff))}";
                ClosingStatusMessage = $"Caja cerrada correctamente. {diffText}.";
                CountedCashText = string.Empty;
            }
            else
            {
                ClosingStatusMessage = $"Error al cerrar caja: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            ClosingStatusMessage = $"Error inesperado: {ex.Message}";
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
