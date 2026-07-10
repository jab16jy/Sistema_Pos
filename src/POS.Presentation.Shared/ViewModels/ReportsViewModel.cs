using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using POS.Application.DTOs.Reporting;
using POS.Application.DTOs.Shifts;
using POS.Application.Interfaces;
using POS.Application.UseCases.Shifts;
using POS.Domain.Enums;
using POS.Presentation.Shared.Commands;

namespace POS.Presentation.Shared.ViewModels;

public sealed class ReportsViewModel : ViewModelBase
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    private const int MaxRecentShiftsToScan = 50;
    private const int MaxRecentClosingsDisplayed = 10;

    private readonly IGetOperationalReportUseCase _reportUseCase;
    private readonly IProfitQueryService? _profitQueryService;
    private readonly IReportExportService? _exportService;
    private readonly GetShiftsUseCase? _getShiftsUseCase;

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

    // Subtab state: 0 = Operacional, 1 = Rentabilidad, 2 = Cierres y arqueos
    private int _activeSubtabIndex;
    private bool _closingsLoaded;
    private ShiftSummary? _lastClosedShift;

    public event EventHandler<(string FileName, byte[] Data)>? FileExportRequested;

    /// <summary>Raised when the user picks an open (pending review) shift from <see cref="PendingShifts"/>.</summary>
    public event Action<Guid>? ShiftOpenRequested;

    public ReportsViewModel(IGetOperationalReportUseCase reportUseCase)
        : this(reportUseCase, null, null, null)
    {
    }

    public ReportsViewModel(IGetOperationalReportUseCase reportUseCase, IProfitQueryService? profitQueryService)
        : this(reportUseCase, profitQueryService, null, null)
    {
    }

    public ReportsViewModel(IGetOperationalReportUseCase reportUseCase, IProfitQueryService? profitQueryService, IReportExportService? exportService)
        : this(reportUseCase, profitQueryService, exportService, null)
    {
    }

    public ReportsViewModel(
        IGetOperationalReportUseCase reportUseCase,
        IProfitQueryService? profitQueryService,
        IReportExportService? exportService,
        GetShiftsUseCase? getShiftsUseCase)
    {
        _reportUseCase = reportUseCase ?? throw new ArgumentNullException(nameof(reportUseCase));
        _profitQueryService = profitQueryService;
        _exportService = exportService;
        _getShiftsUseCase = getShiftsUseCase;

        TopSellers = [];
        TopSellerRows = [];
        PlatformBreakdown = [];
        ProductMargins = [];
        RecentClosings = [];
        PendingShifts = [];

        LoadReportCommand = new RelayCommand(_ => RunSafe(
            LoadReportAsync(
                new DateTimeOffset(_reportFromDate, TimeZoneInfo.Local.GetUtcOffset(_reportFromDate)),
                new DateTimeOffset(_reportToDate, TimeZoneInfo.Local.GetUtcOffset(_reportToDate)).AddDays(1).AddTicks(-1)),
            ex => SetUnhandledError($"Error inesperado al cargar el reporte: {ex.Message}")));

        LoadAnalyticsCommand = new RelayCommand(_ => RunSafe(
            LoadAnalyticsAsync(_analyticsFromDate, _analyticsToDate),
            ex => SetUnhandledError($"Error al cargar analisis: {ex.Message}")));

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
            _ => RunSafe(ExportOperationalAsync(pdf: true), ex => SetUnhandledError($"Error al exportar: {ex.Message}")),
            _ => _exportService is not null && _lastOperationalReport is not null);

        ExportOperationalExcelCommand = new RelayCommand(
            _ => RunSafe(ExportOperationalAsync(pdf: false), ex => SetUnhandledError($"Error al exportar: {ex.Message}")),
            _ => _exportService is not null && _lastOperationalReport is not null);

        ExportProfitPdfCommand = new RelayCommand(
            _ => RunSafe(ExportProfitAsync(pdf: true), ex => SetUnhandledError($"Error al exportar: {ex.Message}")),
            _ => _exportService is not null && _periodComparison is not null);

        ExportProfitExcelCommand = new RelayCommand(
            _ => RunSafe(ExportProfitAsync(pdf: false), ex => SetUnhandledError($"Error al exportar: {ex.Message}")),
            _ => _exportService is not null && _periodComparison is not null);

        LoadClosingsCommand = new RelayCommand(
            _ => RunSafe(LoadClosingsAsync(), ex => SetUnhandledError($"Error al cargar cierres: {ex.Message}")),
            _ => _getShiftsUseCase is not null);

        OpenPendingShiftCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is ShiftRowPresentation row)
                {
                    ShiftOpenRequested?.Invoke(row.Id);
                }
            },
            parameter => parameter is ShiftRowPresentation);
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

    // ── Subtab state (Resumen operacional / Rentabilidad / Cierres y arqueos) ──
    // Note: SubtabBar control has no bindable SelectedItem/content-switching output (documented
    // deviation carried over from Phases 4/6/7/9) — the view uses RadioButtons styled with
    // SubtabItemStyle bound to these three booleans instead.

    public bool IsOperationalSubtabActive
    {
        get => _activeSubtabIndex == 0;
        set { if (value) SetActiveSubtab(0); }
    }

    public bool IsProfitabilitySubtabActive
    {
        get => _activeSubtabIndex == 1;
        set { if (value) SetActiveSubtab(1); }
    }

    public bool IsClosingsSubtabActive
    {
        get => _activeSubtabIndex == 2;
        set
        {
            if (value)
            {
                SetActiveSubtab(2);
                if (!_closingsLoaded)
                {
                    _closingsLoaded = true;
                    RunSafe(LoadClosingsAsync(), ex => SetUnhandledError($"Error al cargar cierres: {ex.Message}"));
                }
            }
        }
    }

    private void SetActiveSubtab(int index)
    {
        if (_activeSubtabIndex == index) return;
        _activeSubtabIndex = index;
        OnPropertyChanged(nameof(IsOperationalSubtabActive));
        OnPropertyChanged(nameof(IsProfitabilitySubtabActive));
        OnPropertyChanged(nameof(IsClosingsSubtabActive));
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
        private set
        {
            if (SetProperty(ref _reportCashCollected, value))
            {
                OnPropertyChanged(nameof(IsDrawerCashBalanced));
                OnPropertyChanged(nameof(CashPaymentPercentText));
                OnPropertyChanged(nameof(DigitalPaymentPercentText));
                OnPropertyChanged(nameof(CashPaymentBarPercent));
                OnPropertyChanged(nameof(DigitalPaymentBarPercent));
            }
        }
    }

    public decimal ReportDigitalCollected
    {
        get => _reportDigitalCollected;
        private set
        {
            if (SetProperty(ref _reportDigitalCollected, value))
            {
                OnPropertyChanged(nameof(CashPaymentPercentText));
                OnPropertyChanged(nameof(DigitalPaymentPercentText));
                OnPropertyChanged(nameof(CashPaymentBarPercent));
                OnPropertyChanged(nameof(DigitalPaymentBarPercent));
            }
        }
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
                OnPropertyChanged(nameof(IsDrawerCashBalanced));
            }
        }
    }

    /// <summary>
    /// Task 13.3: shows the "Cuadra" success chip on the "Efectivo ventas" KPI card when the
    /// expected drawer cash exactly matches the cash collected from sales.
    /// </summary>
    public bool IsDrawerCashBalanced => ReportExpectedDrawerCash == ReportCashCollected;

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

    // ── Payment-method breakdown (13.4): real cash-vs-digital split with percentages.
    // Only two real categories exist in OperationalReportDto (cash and digital); no card/transfer
    // granularity is tracked, so no further categories are invented here. ──

    private decimal PaymentTotal => ReportCashCollected + ReportDigitalCollected;

    public string CashPaymentPercentText => PaymentTotal > 0
        ? $"{ReportCashCollected / PaymentTotal * 100m:N0}%"
        : "0%";

    public string DigitalPaymentPercentText => PaymentTotal > 0
        ? $"{ReportDigitalCollected / PaymentTotal * 100m:N0}%"
        : "0%";

    public double CashPaymentBarPercent => PaymentTotal > 0
        ? (double)(ReportCashCollected / PaymentTotal * 100m)
        : 0d;

    public double DigitalPaymentBarPercent => PaymentTotal > 0
        ? (double)(ReportDigitalCollected / PaymentTotal * 100m)
        : 0d;

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
    public ICommand LoadClosingsCommand { get; }

    // ── Analytics collections ──

    public ObservableCollection<TopSellerResult> TopSellers { get; }

    /// <summary>
    /// Presentation rows for the Top-10 list (13.5): rank + a bar percentage proportional to the
    /// best seller's units (descending, matching <see cref="IProfitQueryService.GetTopSellersAsync"/>'s
    /// real DB ordering).
    /// </summary>
    public ObservableCollection<TopSellerRow> TopSellerRows { get; }

    public PeriodComparisonResult? PeriodComparison
    {
        get => _periodComparison;
        private set
        {
            if (SetProperty(ref _periodComparison, value))
            {
                OnPropertyChanged(nameof(PeriodComparisonIsPositive));
            }
        }
    }

    /// <summary>Growth-percent sign, used for positive/negative styling on the comparison card.</summary>
    public bool PeriodComparisonIsPositive => (_periodComparison?.RevenueGrowthPercent ?? 0m) >= 0m;

    /// <summary>
    /// Desglose por plataforma (13.5): real data from <see cref="IProfitQueryService.GetPlatformBreakdownAsync"/>,
    /// which groups by payment bank/platform (e.g. Nequi, Bancolombia) — not by sales channel.
    /// The wireframe's "POS vs. Cotizaciones convertidas" breakdown has no backing data: Sale has no
    /// quote-origin tracking field, and adding one is a Domain-layer schema change out of scope for this
    /// Presentation-only PR. This panel is relabeled to reflect what the real data represents instead of
    /// inventing a channel split.
    /// </summary>
    public ObservableCollection<PlatformBreakdownResult> PlatformBreakdown { get; }

    public ObservableCollection<ProductMarginResult> ProductMargins { get; }

    // ── Cierres y arqueos (13.6, read-only) ──

    /// <summary>Recent closed shifts, most recent first (real data via <see cref="GetShiftsUseCase"/>).</summary>
    public ObservableCollection<ShiftRowPresentation> RecentClosings { get; }

    /// <summary>Open shifts pending review/close. Selecting one raises <see cref="ShiftOpenRequested"/>
    /// so the shell can navigate to Turnos with that exact shift selected.</summary>
    public ObservableCollection<ShiftRowPresentation> PendingShifts { get; }

    public ICommand OpenPendingShiftCommand { get; }

    public bool HasLastClosedShift => _lastClosedShift is not null;

    public string LastClosedShiftCashier => _lastClosedShift?.CashierUsername ?? "—";

    public string LastClosedShiftOpenSummary => _lastClosedShift is not null
        ? $"{_lastClosedShift.OpenedAt:HH:mm} · Base: {FormatCurrency(_lastClosedShift.OpeningCash)}"
        : "—";

    public string LastClosedShiftCloseSummary => _lastClosedShift?.ClosedAt is { } closedAt
        ? closedAt.ToString("HH:mm · yyyy-MM-dd")
        : "—";

    /// <summary>
    /// Total facturado / Efectivo esperado / Pagos digitales shown on the "Último cierre" card reuse
    /// the Operational subtab's currently loaded report totals (real data, zero new backend work) rather
    /// than a per-shift breakdown — <see cref="Domain.Entities.CashClosing"/> stores those totals per
    /// closing, but <see cref="Domain.Interfaces.ICashClosingRepository"/> only exposes AddAsync, no query
    /// method. Adding one is a reasonable but out-of-scope Infrastructure change for this PR; documented as
    /// a deviation. The view captions this clearly so it is not confused with the shift's own numbers.
    /// </summary>
    public bool HasOperationalTotalsForClosing => _lastOperationalReport is not null;

    public string LastClosingDifferenceLabel => RecentClosings.Count > 0 ? RecentClosings[0].DifferenceLabel : "—";

    public string LastClosingDifferenceKind => RecentClosings.Count > 0 ? RecentClosings[0].DifferenceKind : "neutral";

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
            OnPropertyChanged(nameof(HasOperationalTotalsForClosing));

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
            RaiseExportCommandsCanExecuteChanged();
        }
    }

    // Sibling VMs (Products/Invoices/etc.) all re-raise CanExecuteChanged after state changes; the export
    // buttons' CanExecute depends on _lastOperationalReport/_periodComparison, so without this they stay
    // disabled forever after a load and exporting appears broken.
    private void RaiseExportCommandsCanExecuteChanged()
    {
        ((RelayCommand)ExportOperationalPdfCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ExportOperationalExcelCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ExportProfitPdfCommand).RaiseCanExecuteChanged();
        ((RelayCommand)ExportProfitExcelCommand).RaiseCanExecuteChanged();
    }

    private void ClearTotals()
    {
        ReportBilledTotal = 0m;
        ReportCashCollected = 0m;
        ReportDigitalCollected = 0m;
        ReportReceivableSettlements = 0m;
        ReportExpectedDrawerCash = 0m;
        _lastOperationalReport = null;
        OnPropertyChanged(nameof(HasOperationalTotalsForClosing));
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

            RebuildTopSellerRows();

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
            RaiseExportCommandsCanExecuteChanged();
        }
    }

    private void RebuildTopSellerRows()
    {
        TopSellerRows.Clear();
        int maxUnits = TopSellers.Count > 0 ? TopSellers.Max(t => t.UnitsSold) : 0;
        int rank = 1;
        foreach (var seller in TopSellers)
        {
            double barPercent = maxUnits > 0 ? (double)seller.UnitsSold / maxUnits * 100.0 : 0d;
            TopSellerRows.Add(new TopSellerRow(rank++, seller.ProductName, seller.UnitsSold, seller.TotalRevenue, barPercent));
        }
    }

    /// <summary>
    /// Loads the most recent closed shifts (13.6, read-only) and the open shifts pending review.
    /// Reuses <see cref="GetShiftsUseCase"/> — the same real backend query already wired into Turnos —
    /// and <see cref="ShiftsViewModel.ToShiftRow"/> for consistent difference-coloring conventions
    /// across both views.
    /// </summary>
    public async Task LoadClosingsAsync(CancellationToken ct = default)
    {
        if (_getShiftsUseCase is null)
        {
            return;
        }

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var shifts = await _getShiftsUseCase.ExecuteAsync(MaxRecentShiftsToScan, ct).ConfigureAwait(true);
            var closed = shifts
                .Where(s => s.Status == ShiftStatus.Closed)
                .OrderByDescending(s => s.ClosedAt)
                .Take(MaxRecentClosingsDisplayed)
                .ToList();
            var pending = shifts
                .Where(s => s.Status == ShiftStatus.Open)
                .OrderBy(s => s.OpenedAt)
                .ToList();

            RecentClosings.Clear();
            foreach (var shift in closed)
            {
                RecentClosings.Add(ShiftsViewModel.ToShiftRow(shift));
            }

            PendingShifts.Clear();
            foreach (var shift in pending)
            {
                PendingShifts.Add(ShiftsViewModel.ToShiftRow(shift));
            }

            _lastClosedShift = closed.FirstOrDefault();

            OnPropertyChanged(nameof(HasLastClosedShift));
            OnPropertyChanged(nameof(LastClosedShiftCashier));
            OnPropertyChanged(nameof(LastClosedShiftOpenSummary));
            OnPropertyChanged(nameof(LastClosedShiftCloseSummary));
            OnPropertyChanged(nameof(LastClosingDifferenceLabel));
            OnPropertyChanged(nameof(LastClosingDifferenceKind));
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Error al cargar cierres: {ex.Message}";
            // Allow a retry (manual button or next subtab activation) to actually re-fetch instead
            // of being permanently blocked by the one-load-per-lifetime gate.
            _closingsLoaded = false;
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

    private void SetUnhandledError(string message)
    {
        HasError = true;
        ErrorMessage = message;
    }
}

/// <summary>Presentation row for the Top-10 best-selling products list (13.5).</summary>
public sealed record TopSellerRow(int Rank, string ProductName, int UnitsSold, decimal TotalRevenue, double BarPercent);
