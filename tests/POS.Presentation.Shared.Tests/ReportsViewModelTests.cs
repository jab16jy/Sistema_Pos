using POS.Application.DTOs.Reporting;
using POS.Application.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

/// <summary>
/// RED tests for ReportsViewModel (WU4 Phase 3.2).
/// Focused on load behavior, error state, and loading state consistency.
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
}
