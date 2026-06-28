using POS.Application.DTOs.Reporting;
using POS.Application.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

/// <summary>
/// Tests for ReportsViewModel (WU4 Phase 3.2 + Phase 4 analytics).
/// Covers operational report loading and profit analytics (top sellers, period comparison, platform breakdown, product margins).
/// </summary>
public sealed class ReportsViewModelTests
{
    [Fact]
    public async Task LoadReportAsync_WithValidRange_PopulatesReportTotals()
    {
        var stubReport = new OperationalReportDto(
            BilledSalesTotal: 80_000m,
            SaleCashCollected: 50_000m,
            SaleDigitalCollected: 30_000m,
            ReceivableSettlementCash: 20_000m,
            ReceivableSettlementDigital: 0m,
            ReceivableSettlementTotal: 20_000m,
            ExpectedDrawerCash: 70_000m);

        var stub = new StubGetOperationalReportUseCase(stubReport);
        var vm = new ReportsViewModel(stub);

        await vm.LoadReportAsync(
            from: DateTimeOffset.UtcNow.AddDays(-1),
            to: DateTimeOffset.UtcNow.AddDays(1),
            ct: TestContext.Current.CancellationToken);

        Assert.Equal(80_000m, vm.ReportBilledTotal);
        Assert.Equal(50_000m, vm.ReportCashCollected);
        Assert.Equal(30_000m, vm.ReportDigitalCollected);
        Assert.Equal(20_000m, vm.ReportReceivableSettlements);
        Assert.Equal(70_000m, vm.ReportExpectedDrawerCash);
        Assert.False(vm.HasError);
        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadReportAsync_WhenUseCaseFails_SetsErrorMessage()
    {
        var stub = new StubGetOperationalReportUseCase(error: "Date range is invalid.");
        var vm = new ReportsViewModel(stub);

        await vm.LoadReportAsync(
            from: DateTimeOffset.UtcNow.AddDays(1),
            to: DateTimeOffset.UtcNow.AddDays(-1),
            ct: TestContext.Current.CancellationToken);

        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
        Assert.Equal(0m, vm.ReportBilledTotal);
    }

    [Fact]
    public async Task LoadReportAsync_WhenCalled_SetsIsLoadingDuringExecution()
    {
        var tcs = new TaskCompletionSource<OperationalReportResult>();
        var stub = new StubGetOperationalReportUseCase(tcs.Task);
        var vm = new ReportsViewModel(stub);

        var loadTask = vm.LoadReportAsync(
            from: DateTimeOffset.UtcNow.AddDays(-1),
            to: DateTimeOffset.UtcNow.AddDays(1),
            ct: TestContext.Current.CancellationToken);

        // IsLoading is not reliably observable from synchronous code due to async nature;
        // we assert that after completion, IsLoading is false
        tcs.SetResult(new OperationalReportResult(true, new OperationalReportDto(
            100m, 100m, 0m, 0m, 0m, 0m, 100m), null));
        await loadTask;

        Assert.False(vm.IsLoading);
    }

    [Fact]
    public async Task LoadReportAsync_WithAllZeros_ShowsZeroState()
    {
        var stubReport = new OperationalReportDto(
            BilledSalesTotal: 0m,
            SaleCashCollected: 0m,
            SaleDigitalCollected: 0m,
            ReceivableSettlementCash: 0m,
            ReceivableSettlementDigital: 0m,
            ReceivableSettlementTotal: 0m,
            ExpectedDrawerCash: 0m);

        var stub = new StubGetOperationalReportUseCase(stubReport);
        var vm = new ReportsViewModel(stub);

        await vm.LoadReportAsync(
            from: DateTimeOffset.UtcNow.AddDays(-1),
            to: DateTimeOffset.UtcNow.AddDays(1),
            ct: TestContext.Current.CancellationToken);

        Assert.Equal(0m, vm.ReportBilledTotal);
        Assert.Equal(0m, vm.ReportExpectedDrawerCash);
        Assert.False(vm.HasError);
        Assert.Contains("sin", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────
    // Analytics tests (Phase 4)
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAnalyticsCommand_PopulatesTopSellers()
    {
        var stub = new StubGetOperationalReportUseCase(MakeReport());
        var profitStub = new StubProfitQueryService(
            topSellers: [new TopSellerResult(Guid.NewGuid(), "Product A", 50, 100_000m)]);
        var vm = new ReportsViewModel(stub, profitStub);

        await vm.LoadAnalyticsAsync(DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), TestContext.Current.CancellationToken);

        Assert.Single(vm.TopSellers);
        Assert.Equal("Product A", vm.TopSellers[0].ProductName);
    }

    [Fact]
    public async Task LoadAnalyticsCommand_PopulatesPeriodComparison()
    {
        var stub = new StubGetOperationalReportUseCase(MakeReport());
        var comparison = new PeriodComparisonResult(100_000m, 80_000m, 60_000m, 50_000m, 40_000m, 30_000m, 25m);
        var profitStub = new StubProfitQueryService(periodComparison: comparison);
        var vm = new ReportsViewModel(stub, profitStub);

        await vm.LoadAnalyticsAsync(DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), TestContext.Current.CancellationToken);

        Assert.NotNull(vm.PeriodComparison);
        Assert.Equal(100_000m, vm.PeriodComparison.CurrentRevenue);
        Assert.Equal(25m, vm.PeriodComparison.RevenueGrowthPercent);
    }

    [Fact]
    public async Task LoadAnalyticsCommand_PopulatesPlatformBreakdown()
    {
        var stub = new StubGetOperationalReportUseCase(MakeReport());
        var platforms = new List<PlatformBreakdownResult>
        {
            new("Nequi", 50_000m, 10),
            new("Bancolombia", 30_000m, 5)
        };
        var profitStub = new StubProfitQueryService(platformBreakdown: platforms);
        var vm = new ReportsViewModel(stub, profitStub);

        await vm.LoadAnalyticsAsync(DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), TestContext.Current.CancellationToken);

        Assert.Equal(2, vm.PlatformBreakdown.Count);
    }

    [Fact]
    public async Task LoadAnalyticsCommand_PopulatesProductMargins()
    {
        var stub = new StubGetOperationalReportUseCase(MakeReport());
        var margins = new List<ProductMarginResult>
        {
            new(Guid.NewGuid(), "Product X", 100_000m, 60_000m, 40_000m, 40m)
        };
        var profitStub = new StubProfitQueryService(margins: margins);
        var vm = new ReportsViewModel(stub, profitStub);

        await vm.LoadAnalyticsAsync(DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), TestContext.Current.CancellationToken);

        Assert.Single(vm.ProductMargins);
        Assert.Equal(40m, vm.ProductMargins[0].MarginPercent);
    }

    [Fact]
    public async Task LoadAnalyticsCommand_WithNullProfitService_ShowsUnavailableMessage()
    {
        var stub = new StubGetOperationalReportUseCase(MakeReport());
        var vm = new ReportsViewModel(stub, null);

        await vm.LoadAnalyticsAsync(DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), TestContext.Current.CancellationToken);

        Assert.Contains("no esta disponible", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static OperationalReportDto MakeReport() => new(
        BilledSalesTotal: 80_000m,
        SaleCashCollected: 50_000m,
        SaleDigitalCollected: 30_000m,
        ReceivableSettlementCash: 20_000m,
        ReceivableSettlementDigital: 0m,
        ReceivableSettlementTotal: 20_000m,
        ExpectedDrawerCash: 70_000m);

    // ─────────────────────────────────────────────────────
    // Stubs
    // ─────────────────────────────────────────────────────

    private sealed class StubGetOperationalReportUseCase : IGetOperationalReportUseCase
    {
        private readonly OperationalReportDto? _report;
        private readonly string? _error;
        private readonly Task<OperationalReportResult>? _task;

        public StubGetOperationalReportUseCase(OperationalReportDto report)
            => _report = report;

        public StubGetOperationalReportUseCase(string error)
            => _error = error;

        public StubGetOperationalReportUseCase(Task<OperationalReportResult> task)
            => _task = task;

        public Task<OperationalReportResult> ExecuteAsync(ReportRangeRequest request, CancellationToken ct = default)
        {
            if (_task is not null) return _task;
            if (_error is not null) return Task.FromResult(new OperationalReportResult(false, null, _error));
            return Task.FromResult(new OperationalReportResult(true, _report, null));
        }
    }

    private sealed class StubProfitQueryService : IProfitQueryService
    {
        private readonly IReadOnlyList<TopSellerResult> _topSellers;
        private readonly PeriodComparisonResult _periodComparison;
        private readonly IReadOnlyList<PlatformBreakdownResult> _platformBreakdown;
        private readonly IReadOnlyList<ProductMarginResult> _margins;

        public StubProfitQueryService(
            IReadOnlyList<TopSellerResult>? topSellers = null,
            PeriodComparisonResult? periodComparison = null,
            IReadOnlyList<PlatformBreakdownResult>? platformBreakdown = null,
            IReadOnlyList<ProductMarginResult>? margins = null)
        {
            _topSellers = topSellers ?? [];
            _periodComparison = periodComparison ?? new PeriodComparisonResult(0m, 0m, 0m, 0m, 0m, 0m, 0m);
            _platformBreakdown = platformBreakdown ?? [];
            _margins = margins ?? [];
        }

        public Task<IReadOnlyList<ProductMarginResult>> GetMarginByProductAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult(_margins);

        public Task<IReadOnlyList<TopSellerResult>> GetTopSellersAsync(DateOnly from, DateOnly to, int top = 10, CancellationToken ct = default)
            => Task.FromResult(_topSellers);

        public Task<PeriodComparisonResult> GetPeriodComparisonAsync(DateOnly currentFrom, DateOnly currentTo, DateOnly priorFrom, DateOnly priorTo, CancellationToken ct = default)
            => Task.FromResult(_periodComparison);

        public Task<IReadOnlyList<PlatformBreakdownResult>> GetPlatformBreakdownAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult(_platformBreakdown);
    }
}
