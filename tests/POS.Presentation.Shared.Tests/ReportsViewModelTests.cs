using POS.Application.DTOs.Reporting;
using POS.Application.Interfaces;
using POS.Application.UseCases.Shifts;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
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
            ExpectedDrawerCash: 70_000m,
            ChartPoints: Array.Empty<OperationalReportChartPointDto>());

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
    public async Task LoadReportAsync_RaisesExportCommandCanExecuteChanged()
    {
        // Regression: export buttons stayed disabled forever because CanExecuteChanged never fired
        // after a report loaded, so WPF never re-queried CanExecute and PDF/Excel export looked broken.
        var stubReport = new OperationalReportDto(
            BilledSalesTotal: 80_000m,
            SaleCashCollected: 50_000m,
            SaleDigitalCollected: 30_000m,
            ReceivableSettlementCash: 20_000m,
            ReceivableSettlementDigital: 0m,
            ReceivableSettlementTotal: 20_000m,
            ExpectedDrawerCash: 70_000m,
            ChartPoints: Array.Empty<OperationalReportChartPointDto>());

        var vm = new ReportsViewModel(
            new StubGetOperationalReportUseCase(stubReport),
            profitQueryService: null,
            exportService: new FakeReportExportService());

        bool raised = false;
        vm.ExportOperationalPdfCommand.CanExecuteChanged += (_, _) => raised = true;

        await vm.LoadReportAsync(
            from: DateTimeOffset.UtcNow.AddDays(-1),
            to: DateTimeOffset.UtcNow.AddDays(1),
            ct: TestContext.Current.CancellationToken);

        Assert.True(raised);
        Assert.True(vm.ExportOperationalPdfCommand.CanExecute(null));
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
            100m, 100m, 0m, 0m, 0m, 0m, 100m, Array.Empty<OperationalReportChartPointDto>()), null));
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
            ExpectedDrawerCash: 0m,
            ChartPoints: Array.Empty<OperationalReportChartPointDto>());

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

    [Fact]
    public async Task LoadAnalyticsAsync_WhenProfitServiceThrows_SetsHasErrorAndErrorMessage()
    {
        var stub = new StubGetOperationalReportUseCase(MakeReport());
        var vm = new ReportsViewModel(stub, new ThrowingProfitQueryService());

        await vm.LoadAnalyticsAsync(DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), TestContext.Current.CancellationToken);

        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
    }

    // ─────────────────────────────────────────────────────
    // Task 13.3: "Cuadra" chip + payment-method breakdown (Task 13.4)
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task LoadReportAsync_WhenExpectedEqualsCashCollected_SetsIsDrawerCashBalancedTrue()
    {
        var stubReport = new OperationalReportDto(
            BilledSalesTotal: 100_000m,
            SaleCashCollected: 60_000m,
            SaleDigitalCollected: 40_000m,
            ReceivableSettlementCash: 0m,
            ReceivableSettlementDigital: 0m,
            ReceivableSettlementTotal: 0m,
            ExpectedDrawerCash: 60_000m,
            ChartPoints: Array.Empty<OperationalReportChartPointDto>());

        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(stubReport));

        await vm.LoadReportAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            TestContext.Current.CancellationToken);

        Assert.True(vm.IsDrawerCashBalanced);
    }

    [Fact]
    public async Task LoadReportAsync_WhenExpectedDiffersFromCashCollected_SetsIsDrawerCashBalancedFalse()
    {
        var stubReport = new OperationalReportDto(
            BilledSalesTotal: 100_000m,
            SaleCashCollected: 60_000m,
            SaleDigitalCollected: 40_000m,
            ReceivableSettlementCash: 10_000m,
            ReceivableSettlementDigital: 0m,
            ReceivableSettlementTotal: 10_000m,
            ExpectedDrawerCash: 70_000m,
            ChartPoints: Array.Empty<OperationalReportChartPointDto>());

        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(stubReport));

        await vm.LoadReportAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            TestContext.Current.CancellationToken);

        Assert.False(vm.IsDrawerCashBalanced);
    }

    [Fact]
    public async Task LoadReportAsync_ComputesPaymentMethodPercentages()
    {
        var stubReport = new OperationalReportDto(
            BilledSalesTotal: 100_000m,
            SaleCashCollected: 75_000m,
            SaleDigitalCollected: 25_000m,
            ReceivableSettlementCash: 0m,
            ReceivableSettlementDigital: 0m,
            ReceivableSettlementTotal: 0m,
            ExpectedDrawerCash: 75_000m,
            ChartPoints: Array.Empty<OperationalReportChartPointDto>());

        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(stubReport));

        await vm.LoadReportAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            TestContext.Current.CancellationToken);

        Assert.Equal("75%", vm.CashPaymentPercentText);
        Assert.Equal("25%", vm.DigitalPaymentPercentText);
        Assert.Equal(75d, vm.CashPaymentBarPercent);
        Assert.Equal(25d, vm.DigitalPaymentBarPercent);
    }

    [Fact]
    public async Task LoadReportAsync_WhenPaymentTotalIsZero_FallsBackWithoutDividingByZero()
    {
        var stubReport = new OperationalReportDto(
            BilledSalesTotal: 0m,
            SaleCashCollected: 0m,
            SaleDigitalCollected: 0m,
            ReceivableSettlementCash: 0m,
            ReceivableSettlementDigital: 0m,
            ReceivableSettlementTotal: 0m,
            ExpectedDrawerCash: 0m,
            ChartPoints: Array.Empty<OperationalReportChartPointDto>());

        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(stubReport));

        await vm.LoadReportAsync(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1),
            TestContext.Current.CancellationToken);

        Assert.Equal("0%", vm.CashPaymentPercentText);
        Assert.Equal("0%", vm.DigitalPaymentPercentText);
        Assert.Equal(0d, vm.CashPaymentBarPercent);
        Assert.Equal(0d, vm.DigitalPaymentBarPercent);
    }

    // ─────────────────────────────────────────────────────
    // Task 13.5: Top-10 proportional bar rows
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task LoadAnalyticsCommand_WithEmptyTopSellers_ProducesEmptyTopSellerRows()
    {
        var stub = new StubGetOperationalReportUseCase(MakeReport());
        var profitStub = new StubProfitQueryService(topSellers: []);
        var vm = new ReportsViewModel(stub, profitStub);

        await vm.LoadAnalyticsAsync(DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), TestContext.Current.CancellationToken);

        Assert.Empty(vm.TopSellerRows);
    }

    [Fact]
    public async Task LoadAnalyticsCommand_PopulatesTopSellerRowsWithProportionalBarPercent()
    {
        var stub = new StubGetOperationalReportUseCase(MakeReport());
        var profitStub = new StubProfitQueryService(topSellers:
        [
            new TopSellerResult(Guid.NewGuid(), "Producto A", 40, 200_000m),
            new TopSellerResult(Guid.NewGuid(), "Producto B", 20, 100_000m)
        ]);
        var vm = new ReportsViewModel(stub, profitStub);

        await vm.LoadAnalyticsAsync(DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today), TestContext.Current.CancellationToken);

        Assert.Equal(2, vm.TopSellerRows.Count);
        Assert.Equal(1, vm.TopSellerRows[0].Rank);
        Assert.Equal(100d, vm.TopSellerRows[0].BarPercent);
        Assert.Equal(2, vm.TopSellerRows[1].Rank);
        Assert.Equal(50d, vm.TopSellerRows[1].BarPercent);
    }

    // ─────────────────────────────────────────────────────
    // Task 13.6: Cierres y arqueos (read-only closings query)
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task LoadClosingsAsync_PopulatesRecentClosingsFromClosedShiftsOnly()
    {
        var closedShift = new CashierShift(Guid.NewGuid(), "Ana", new Money(100_000m));
        closedShift.Close(new Money(100_000m));
        var openShift = new CashierShift(Guid.NewGuid(), "Luis", new Money(50_000m));

        var repo = new StubCashierShiftRepository([closedShift, openShift]);
        var getShifts = new GetShiftsUseCase(repo);
        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(MakeReport()), null, null, getShifts);

        await vm.LoadClosingsAsync(TestContext.Current.CancellationToken);

        Assert.Single(vm.RecentClosings);
        Assert.Equal("Ana", vm.RecentClosings[0].CashierUsername);
        Assert.True(vm.HasLastClosedShift);
        Assert.Equal("Ana", vm.LastClosedShiftCashier);
        Assert.Equal("Cuadra", vm.LastClosingDifferenceLabel);
        Assert.Equal("success", vm.LastClosingDifferenceKind);
    }

    [Fact]
    public async Task LoadClosingsAsync_PopulatesPendingShiftsFromOpenShiftsOnly()
    {
        var closedShift = new CashierShift(Guid.NewGuid(), "Ana", new Money(100_000m));
        closedShift.Close(new Money(100_000m));
        var openShift = new CashierShift(Guid.NewGuid(), "Luis", new Money(50_000m));

        var repo = new StubCashierShiftRepository([closedShift, openShift]);
        var getShifts = new GetShiftsUseCase(repo);
        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(MakeReport()), null, null, getShifts);

        await vm.LoadClosingsAsync(TestContext.Current.CancellationToken);

        Assert.Single(vm.PendingShifts);
        Assert.Equal("Luis", vm.PendingShifts[0].CashierUsername);
        Assert.Equal(openShift.Id, vm.PendingShifts[0].Id);
    }

    [Fact]
    public void OpenPendingShiftCommand_RaisesShiftOpenRequestedWithRowId()
    {
        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(MakeReport()));
        var shiftId = Guid.NewGuid();
        var row = new ShiftRowPresentation(shiftId, "Luis", "Abierto", "success", "2026-01-01 08:00", "—", "$50.000", "—", "—", "neutral", true);

        Guid? raised = null;
        vm.ShiftOpenRequested += id => raised = id;

        Assert.True(vm.OpenPendingShiftCommand.CanExecute(row));
        vm.OpenPendingShiftCommand.Execute(row);

        Assert.Equal(shiftId, raised);
    }

    [Fact]
    public async Task LoadClosingsAsync_WithNoClosedShifts_LeavesHasLastClosedShiftFalse()
    {
        var repo = new StubCashierShiftRepository([]);
        var getShifts = new GetShiftsUseCase(repo);
        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(MakeReport()), null, null, getShifts);

        await vm.LoadClosingsAsync(TestContext.Current.CancellationToken);

        Assert.Empty(vm.RecentClosings);
        Assert.False(vm.HasLastClosedShift);
        Assert.Equal("—", vm.LastClosedShiftCashier);
    }

    [Fact]
    public async Task LoadClosingsAsync_WithNullUseCase_DoesNotThrow()
    {
        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(MakeReport()));

        await vm.LoadClosingsAsync(TestContext.Current.CancellationToken);

        Assert.Empty(vm.RecentClosings);
        Assert.False(vm.HasLastClosedShift);
    }

    [Fact]
    public async Task LoadClosingsAsync_WhenRepositoryThrows_SetsHasErrorAndErrorMessage()
    {
        var repo = new FlakyCashierShiftRepository([], shouldThrowFirst: true);
        var getShifts = new GetShiftsUseCase(repo);
        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(MakeReport()), null, null, getShifts);

        await vm.LoadClosingsAsync(TestContext.Current.CancellationToken);

        Assert.True(vm.HasError);
        Assert.False(string.IsNullOrWhiteSpace(vm.ErrorMessage));
    }

    [Fact]
    public async Task LoadClosingsAsync_WithMoreThanMaxDisplayed_TruncatesToTenMostRecent()
    {
        var shifts = new List<CashierShift>();
        for (int i = 0; i < 12; i++)
        {
            var shift = new CashierShift(Guid.NewGuid(), $"Cajero{i}", new Money(100_000m));
            shift.Close(new Money(100_000m));
            shifts.Add(shift);
        }

        var repo = new StubCashierShiftRepository(shifts);
        var getShifts = new GetShiftsUseCase(repo);
        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(MakeReport()), null, null, getShifts);

        await vm.LoadClosingsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(10, vm.RecentClosings.Count);
    }

    [Fact]
    public async Task LoadClosingsAsync_WithNonChronologicalInput_ReordersByClosedAtDescending()
    {
        var shiftOldest = new CashierShift(Guid.NewGuid(), "Oldest", new Money(100_000m));
        var shiftMiddle = new CashierShift(Guid.NewGuid(), "Middle", new Money(100_000m));
        var shiftNewest = new CashierShift(Guid.NewGuid(), "Newest", new Money(100_000m));

        // Close out of list order so ClosedAt does not match the array's insertion order.
        shiftOldest.Close(new Money(100_000m));
        await Task.Delay(20, TestContext.Current.CancellationToken);
        shiftMiddle.Close(new Money(100_000m));
        await Task.Delay(20, TestContext.Current.CancellationToken);
        shiftNewest.Close(new Money(100_000m));

        // Input order intentionally does not match the expected descending-by-ClosedAt output order.
        var repo = new StubCashierShiftRepository([shiftMiddle, shiftOldest, shiftNewest]);
        var getShifts = new GetShiftsUseCase(repo);
        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(MakeReport()), null, null, getShifts);

        await vm.LoadClosingsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, vm.RecentClosings.Count);
        Assert.Equal("Newest", vm.RecentClosings[0].CashierUsername);
        Assert.Equal("Middle", vm.RecentClosings[1].CashierUsername);
        Assert.Equal("Oldest", vm.RecentClosings[2].CashierUsername);
    }

    [Fact]
    public async Task LoadClosingsAsync_ProjectsSurplusAndShortageDifferenceBranches()
    {
        var surplusShift = new CashierShift(Guid.NewGuid(), "Surplus", new Money(100_000m));
        surplusShift.Close(new Money(120_000m));

        var shortageShift = new CashierShift(Guid.NewGuid(), "Shortage", new Money(100_000m));
        shortageShift.Close(new Money(80_000m));

        var repo = new StubCashierShiftRepository([surplusShift, shortageShift]);
        var getShifts = new GetShiftsUseCase(repo);
        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(MakeReport()), null, null, getShifts);

        await vm.LoadClosingsAsync(TestContext.Current.CancellationToken);

        // Formatted with the ambient culture, same as ShiftsViewModel.ToShiftRow itself, to avoid a
        // hardcoded culture-specific literal.
        var surplusRow = vm.RecentClosings.Single(r => r.CashierUsername == "Surplus");
        Assert.Equal("primary", surplusRow.DifferenceKind);
        Assert.Equal($"+${20_000m:N0}", surplusRow.DifferenceLabel);

        var shortageRow = vm.RecentClosings.Single(r => r.CashierUsername == "Shortage");
        Assert.Equal("error", shortageRow.DifferenceKind);
        Assert.Equal($"-${20_000m:N0}", shortageRow.DifferenceLabel);
    }

    [Fact]
    public void LoadClosingsCommand_AfterFailure_RetrySucceedsInsteadOfBeingBlocked()
    {
        var closedShift = new CashierShift(Guid.NewGuid(), "Ana", new Money(100_000m));
        closedShift.Close(new Money(100_000m));

        var repo = new FlakyCashierShiftRepository([closedShift], shouldThrowFirst: true);
        var getShifts = new GetShiftsUseCase(repo);
        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(MakeReport()), null, null, getShifts);

        // First attempt fails.
        vm.LoadClosingsCommand.Execute(null);
        Assert.True(vm.HasError);
        Assert.Empty(vm.RecentClosings);

        // Retry via the command (not gated by the one-load-per-lifetime flag) should now succeed.
        vm.LoadClosingsCommand.Execute(null);
        Assert.False(vm.HasError);
        Assert.Single(vm.RecentClosings);
        Assert.Equal("Ana", vm.RecentClosings[0].CashierUsername);
    }

    // ─────────────────────────────────────────────────────
    // Subtab selection state
    // ─────────────────────────────────────────────────────

    [Fact]
    public void SubtabState_DefaultsToOperational()
    {
        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(MakeReport()));

        Assert.True(vm.IsOperationalSubtabActive);
        Assert.False(vm.IsProfitabilitySubtabActive);
        Assert.False(vm.IsClosingsSubtabActive);
    }

    [Fact]
    public void SubtabState_SettingRentabilidadDeactivatesOthers()
    {
        var vm = new ReportsViewModel(new StubGetOperationalReportUseCase(MakeReport()))
        {
            IsProfitabilitySubtabActive = true
        };

        Assert.False(vm.IsOperationalSubtabActive);
        Assert.True(vm.IsProfitabilitySubtabActive);
        Assert.False(vm.IsClosingsSubtabActive);
    }

    private static OperationalReportDto MakeReport() => new(
        BilledSalesTotal: 80_000m,
        SaleCashCollected: 50_000m,
        SaleDigitalCollected: 30_000m,
        ReceivableSettlementCash: 20_000m,
        ReceivableSettlementDigital: 0m,
        ReceivableSettlementTotal: 20_000m,
        ExpectedDrawerCash: 70_000m,
        ChartPoints: Array.Empty<OperationalReportChartPointDto>());

    // ─────────────────────────────────────────────────────
    // Stubs
    // ─────────────────────────────────────────────────────

    private sealed class FakeReportExportService : IReportExportService
    {
        public Task<byte[]> ExportOperationalReportPdfAsync(OperationalReportDto report, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> ExportOperationalReportExcelAsync(OperationalReportDto report, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> ExportProfitReportPdfAsync(IReadOnlyList<TopSellerResult> topSellers, PeriodComparisonResult comparison, IReadOnlyList<ProductMarginResult> margins, DateOnly from, DateOnly to, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());
        public Task<byte[]> ExportProfitReportExcelAsync(IReadOnlyList<TopSellerResult> topSellers, PeriodComparisonResult comparison, IReadOnlyList<ProductMarginResult> margins, DateOnly from, DateOnly to, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());
    }

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

    /// <summary>Always throws from <see cref="GetMarginByProductAsync"/> to simulate an analytics-load failure.</summary>
    private sealed class ThrowingProfitQueryService : IProfitQueryService
    {
        public Task<IReadOnlyList<ProductMarginResult>> GetMarginByProductAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
            => throw new InvalidOperationException("Simulated analytics failure.");

        public Task<IReadOnlyList<TopSellerResult>> GetTopSellersAsync(DateOnly from, DateOnly to, int top = 10, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TopSellerResult>>([]);

        public Task<PeriodComparisonResult> GetPeriodComparisonAsync(DateOnly currentFrom, DateOnly currentTo, DateOnly priorFrom, DateOnly priorTo, CancellationToken ct = default)
            => Task.FromResult(new PeriodComparisonResult(0m, 0m, 0m, 0m, 0m, 0m, 0m));

        public Task<IReadOnlyList<PlatformBreakdownResult>> GetPlatformBreakdownAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PlatformBreakdownResult>>([]);
    }

    private sealed class StubCashierShiftRepository : ICashierShiftRepository
    {
        private readonly IReadOnlyList<CashierShift> _shifts;

        public StubCashierShiftRepository(IReadOnlyList<CashierShift> shifts) => _shifts = shifts;

        public Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(_shifts.FirstOrDefault(s => s.CashierUserId == cashierUserId));

        public Task<CashierShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_shifts.FirstOrDefault(s => s.Id == id));

        public Task<IReadOnlyList<CashierShift>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult(_shifts);

        public Task AddAsync(CashierShift shift, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>
    /// Throws once (on the first <see cref="GetRecentAsync"/> call) when <c>shouldThrowFirst</c> is true,
    /// then succeeds on every subsequent call — used to verify the Cierres y arqueos retry path actually
    /// re-fetches instead of being permanently blocked by the one-load-per-lifetime gate.
    /// </summary>
    private sealed class FlakyCashierShiftRepository : ICashierShiftRepository
    {
        private readonly IReadOnlyList<CashierShift> _shifts;
        private bool _shouldThrow;

        public FlakyCashierShiftRepository(IReadOnlyList<CashierShift> shifts, bool shouldThrowFirst)
        {
            _shifts = shifts;
            _shouldThrow = shouldThrowFirst;
        }

        public Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
            => Task.FromResult(_shifts.FirstOrDefault(s => s.CashierUserId == cashierUserId));

        public Task<CashierShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_shifts.FirstOrDefault(s => s.Id == id));

        public Task<IReadOnlyList<CashierShift>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
        {
            if (_shouldThrow)
            {
                _shouldThrow = false;
                throw new InvalidOperationException("Simulated transient closings failure.");
            }

            return Task.FromResult(_shifts);
        }

        public Task AddAsync(CashierShift shift, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
