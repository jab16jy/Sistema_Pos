using System.Linq;
using POS.Application.DTOs.Shifts;
using POS.Application.Interfaces;
using POS.Application.UseCases.Shifts;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class ShiftsViewModelTests
{
    [Fact]
    public async Task LoadCommand_WhenShiftQueryFails_SetsRecoverableMessage()
    {
        var repository = new ThrowingShiftRepository();
        var viewModel = new ShiftsViewModel(
            new StartShiftUseCase(repository, new NoOpUnitOfWork()),
            new CloseShiftUseCase(repository, new NoOpUnitOfWork()),
            new GetShiftsUseCase(repository),
            new StubOperatorSession());

        viewModel.LoadCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar turnos", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_WithTodayDateFilter_ListsShiftWithoutOffsetError()
    {
        // Regression: ApplyDateFilter built `new DateTimeOffset(localDate, TimeSpan.Zero)`, which
        // throws on any non-UTC machine, leaving FilteredShifts empty ("ghost shift") and the
        // filter action crashing the app. FromDate/ToDate default to DateTime.Today (Kind=Local).
        var shift = new CashierShift(Guid.NewGuid(), "cajero", new Money(100m), null);
        var repository = new SingleShiftRepository(shift);
        var viewModel = new ShiftsViewModel(
            new StartShiftUseCase(repository, new NoOpUnitOfWork()),
            new CloseShiftUseCase(repository, new NoOpUnitOfWork()),
            new GetShiftsUseCase(repository),
            new StubOperatorSession());

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Single(viewModel.FilteredShifts);
        Assert.DoesNotContain("Error", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SelectShiftAsync_ClearsDateFilterAndSelectsShiftById()
    {
        var shift = new CashierShift(Guid.NewGuid(), "cajero", new Money(100m), null);
        var repository = new SingleShiftRepository(shift);
        var viewModel = new ShiftsViewModel(
            new StartShiftUseCase(repository, new NoOpUnitOfWork()),
            new CloseShiftUseCase(repository, new NoOpUnitOfWork()),
            new GetShiftsUseCase(repository),
            new StubOperatorSession());

        // Narrow the filter to a past window that would exclude today's shift, mirroring the redirect
        // from Reportes landing on Turnos with whatever filter was left over from a previous visit.
        viewModel.FromDate = DateTime.Today.AddDays(-30);
        viewModel.ToDate = DateTime.Today.AddDays(-29);

        await viewModel.SelectShiftAsync(shift.Id, TestContext.Current.CancellationToken);

        Assert.Null(viewModel.FromDate);
        Assert.Null(viewModel.ToDate);
        // ShiftsView binds the grid's SelectedItem to SelectedShiftRow (a ShiftRowPresentation, the
        // FilteredShifts element type) — not to SelectedShift (a ShiftSummary), which nothing in the
        // View reads. Asserting SelectedShiftRow is the SAME instance already sitting in FilteredShifts
        // is what actually proves the ListBox can resolve and highlight it; a different-but-equal
        // instance would not satisfy WPF's SelectedItem match against ItemsSource.
        Assert.NotNull(viewModel.SelectedShiftRow);
        Assert.Equal(shift.Id, viewModel.SelectedShiftRow!.Id);
        Assert.Contains(viewModel.SelectedShiftRow, viewModel.FilteredShifts);
        Assert.Same(viewModel.FilteredShifts.Single(r => r.Id == shift.Id), viewModel.SelectedShiftRow);
        Assert.NotNull(viewModel.SelectedShift);
        Assert.Equal(shift.Id, viewModel.SelectedShift!.Id);
    }

    private sealed class SingleShiftRepository : ICashierShiftRepository
    {
        private readonly CashierShift _shift;

        public SingleShiftRepository(CashierShift shift) => _shift = shift;

        public Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(null);

        public Task<CashierShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(_shift);

        public Task<IReadOnlyList<CashierShift>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CashierShift>>([_shift]);

        public Task AddAsync(CashierShift shift, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ThrowingShiftRepository : ICashierShiftRepository
    {
        public Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(null);

        public Task<CashierShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(null);

        public Task<IReadOnlyList<CashierShift>> GetRecentAsync(int limit = 50, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<CashierShift>>(new InvalidOperationException("Consulta falló"));

        public Task AddAsync(CashierShift shift, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class StubOperatorSession : ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = Guid.NewGuid();
        public string Username { get; } = "cajero";
        public UserRole Role { get; } = UserRole.Cashier;
        public bool IsAuthenticated => true;
    }
}
