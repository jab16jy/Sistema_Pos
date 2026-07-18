using System.Linq;
using POS.Application.DTOs.Shifts;
using POS.Application.Interfaces;
using POS.Application.Services;
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
        var session = new StubOperatorSession();
        var viewModel = new ShiftsViewModel(
            new StartShiftUseCase(repository, new NoOpUnitOfWork(), CreateAuthorizer(session)),
            new CloseShiftUseCase(repository, new SummarySaleRepository(ShiftSalesSummaryData.Empty), new NoOpUnitOfWork(), CreateAuthorizer(session)),
            new GetShiftsUseCase(repository),
            new GetShiftSalesSummaryUseCase(new SummarySaleRepository(new ShiftSalesSummaryData(4_200m, 2_800m, 1_400m))),
            session,
            new StubUserRepository());

        viewModel.LoadCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Contains("Error al cargar turnos", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_WithDefaultHistoryFilter_ListsShiftWithoutRestrictingToToday()
    {
        // The history screen must start unfiltered. Defaulting both dates to today hid every
        // persisted shift opened on a previous day.
        var shift = new CashierShift(Guid.NewGuid(), "cajero", new Money(100m), null);
        var repository = new SingleShiftRepository(shift);
        var session = new StubOperatorSession();
        var viewModel = new ShiftsViewModel(
            new StartShiftUseCase(repository, new NoOpUnitOfWork(), CreateAuthorizer(session)),
            new CloseShiftUseCase(repository, new SummarySaleRepository(ShiftSalesSummaryData.Empty), new NoOpUnitOfWork(), CreateAuthorizer(session)),
            new GetShiftsUseCase(repository),
            new GetShiftSalesSummaryUseCase(new SummarySaleRepository(new ShiftSalesSummaryData(4_200m, 2_800m, 1_400m))),
            session,
            new StubUserRepository());

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Single(viewModel.FilteredShifts);
        Assert.Null(viewModel.FromDate);
        Assert.Null(viewModel.ToDate);
        Assert.Equal("$4.200", viewModel.ActiveShiftSalesTotal);
        Assert.Equal(2_900m, viewModel.ExpectedCash);
        Assert.Equal("$1.400", viewModel.ExpectedDigitalFormatted);
        Assert.Equal(2_900m, viewModel.CountedCash);
        Assert.DoesNotContain("Error", viewModel.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_WithReturnOutflows_SubtractsOnlyCashFromExpectedDrawer()
    {
        var shift = new CashierShift(Guid.NewGuid(), "cajero", new Money(100m), null);
        var repository = new SingleShiftRepository(shift);
        var session = new StubOperatorSession();
        var viewModel = new ShiftsViewModel(
            new StartShiftUseCase(repository, new NoOpUnitOfWork(), CreateAuthorizer(session)),
            new CloseShiftUseCase(repository, new SummarySaleRepository(ShiftSalesSummaryData.Empty), new NoOpUnitOfWork(), CreateAuthorizer(session)),
            new GetShiftsUseCase(repository),
            new GetShiftSalesSummaryUseCase(new SummarySaleRepository(new ShiftSalesSummaryData(
                SalesTotal: 4_200m,
                CashPayments: 2_800m,
                DigitalPayments: 1_400m,
                CashReturnOutflows: 600m,
                DigitalReturnOutflows: 700m))),
            session,
            new StubUserRepository());

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2_300m, viewModel.ExpectedCash);
        Assert.Equal("$1.400", viewModel.ExpectedDigitalFormatted);
        Assert.Equal(2_300m, viewModel.CountedCash);
    }

    [Fact]
    public async Task SelectShiftAsync_ClearsDateFilterAndSelectsShiftById()
    {
        var shift = new CashierShift(Guid.NewGuid(), "cajero", new Money(100m), null);
        var repository = new SingleShiftRepository(shift);
        var session = new StubOperatorSession();
        var viewModel = new ShiftsViewModel(
            new StartShiftUseCase(repository, new NoOpUnitOfWork(), CreateAuthorizer(session)),
            new CloseShiftUseCase(repository, new EmptySaleRepository(), new NoOpUnitOfWork(), CreateAuthorizer(session)),
            new GetShiftsUseCase(repository),
            new GetShiftSalesSummaryUseCase(new EmptySaleRepository()),
            session,
            new StubUserRepository());

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

    [Fact]
    public void StartShiftCommand_WithInvalidOpeningTime_ShowsFormatMessage()
    {
        var repository = new CapturingShiftRepository();
        var session = new StubOperatorSession();
        var viewModel = new ShiftsViewModel(
            new StartShiftUseCase(repository, new NoOpUnitOfWork(), CreateAuthorizer(session)),
            new CloseShiftUseCase(repository, new SummarySaleRepository(ShiftSalesSummaryData.Empty), new NoOpUnitOfWork(), CreateAuthorizer(session)),
            new GetShiftsUseCase(repository),
            new GetShiftSalesSummaryUseCase(new SummarySaleRepository(ShiftSalesSummaryData.Empty)),
            session,
            new StubUserRepository())
        {
            OpeningTime = "9:5"
        };

        viewModel.StartShiftCommand.Execute(null);

        Assert.Contains("HH:mm", viewModel.Message, StringComparison.Ordinal);
        Assert.Null(repository.AddedShift);
    }

    [Fact]
    public async Task StartShiftCommand_WithSelectedDateAndTime_PassesLocalTimestampToCreatedShift()
    {
        var repository = new CapturingShiftRepository();
        var session = new StubOperatorSession();
        var viewModel = new ShiftsViewModel(
            new StartShiftUseCase(repository, new NoOpUnitOfWork(), CreateAuthorizer(session)),
            new CloseShiftUseCase(repository, new SummarySaleRepository(ShiftSalesSummaryData.Empty), new NoOpUnitOfWork(), CreateAuthorizer(session)),
            new GetShiftsUseCase(repository),
            new GetShiftSalesSummaryUseCase(new SummarySaleRepository(ShiftSalesSummaryData.Empty)),
            session,
            new StubUserRepository());
        var selectedDate = new DateTime(2020, 5, 6);
        viewModel.OpeningDate = selectedDate;
        viewModel.OpeningTime = "09:17";

        viewModel.StartShiftCommand.Execute(null);
        for (var attempt = 0; attempt < 20 && repository.AddedShift is null; attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        Assert.NotNull(repository.AddedShift);
        var localDateTime = DateTime.SpecifyKind(
            selectedDate.AddHours(9).AddMinutes(17),
            DateTimeKind.Unspecified);
        var expected = new DateTimeOffset(
            localDateTime,
            TimeZoneInfo.Local.GetUtcOffset(localDateTime)).ToUniversalTime();
        Assert.Equal(expected, repository.AddedShift!.OpenedAt);
    }

    [Fact]
    public void ToShiftRow_WithClosingSummary_UsesExpectedCashForReconciliation()
    {
        var summary = new ShiftSummary(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "cajero",
            100m,
            2_900m,
            ShiftStatus.Closed,
            DateTimeOffset.UtcNow.AddHours(-2),
            DateTimeOffset.UtcNow,
            null,
            HasClosingSummary: true,
            SalesTotal: 4_200m,
            CashPayments: 2_800m,
            DigitalPayments: 1_400m);

        ShiftRowPresentation row = ShiftsViewModel.ToShiftRow(summary);

        Assert.Equal("$4.200", row.SalesTotalFormatted);
        Assert.Equal("Digital: $1.400", row.DigitalPaymentsFormatted);
        Assert.Equal("$2.900", row.ExpectedCashFormatted);
        Assert.Equal("Cuadra", row.DifferenceLabel);
    }

    private sealed class CapturingShiftRepository : ICashierShiftRepository
    {
        public CashierShift? AddedShift { get; private set; }

        public Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(null);

        public Task<CashierShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(AddedShift);

        public Task<IReadOnlyList<CashierShift>> GetRecentAsync(int limit = 50, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CashierShift>>(AddedShift is { } shift ? [shift] : []);

        public Task AddAsync(CashierShift shift, CancellationToken cancellationToken = default)
        {
            AddedShift = shift;
            return Task.CompletedTask;
        }
    }

    private sealed class SingleShiftRepository : ICashierShiftRepository
    {
        private readonly CashierShift _shift;

        public SingleShiftRepository(CashierShift shift) => _shift = shift;

        public Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(null);

        public Task<CashierShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(_shift);

        public Task<IReadOnlyList<CashierShift>> GetRecentAsync(int limit = 50, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CashierShift>>([_shift]);

        public Task AddAsync(CashierShift shift, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ThrowingShiftRepository : ICashierShiftRepository
    {
        public Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(null);

        public Task<CashierShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(null);

        public Task<IReadOnlyList<CashierShift>> GetRecentAsync(int limit = 50, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<CashierShift>>(new InvalidOperationException("Consulta falló"));

        public Task AddAsync(CashierShift shift, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubUserRepository(User? user = null) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(user?.Id == id ? user : null);
        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>([]);
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class EmptySaleRepository : ISaleRepository
    {
        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Sale?>(null);

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));
    }

    private sealed class SummarySaleRepository(ShiftSalesSummaryData summary) : ISaleRepository
    {
        public Task AddAsync(Sale sale, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Sale?>(null);

        public Task<DashboardSummaryData> GetDashboardSummaryAsync(
            DateTimeOffset from,
            DateTimeOffset to,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DashboardSummaryData(0m, 0m, 0m, 0, 0));

        public Task<ShiftSalesSummaryData> GetShiftSalesSummaryAsync(
            Guid cashierUserId,
            DateTimeOffset openedAt,
            CancellationToken cancellationToken = default)
            => Task.FromResult(summary);
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class StubOperatorSession : ICurrentOperatorSession
    {
        public User User { get; } = new("cajero", "hash", Guid.NewGuid());
        public Guid OperatorId => User.Id;
        public string Username => User.Username;
        public Guid RoleId { get; } = POS.Domain.Authorization.RoleConstants.CashierRoleId;
        public string RoleName => POS.Domain.Authorization.RoleConstants.CashierRoleName;
        public IReadOnlySet<string> Permissions => new HashSet<string>(POS.Domain.Authorization.PermissionKeys.CashierDefaults);
        public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
        public bool IsAuthenticated => true;
    }

    private static CriticalCommandAuthorizer CreateAuthorizer(StubOperatorSession session) =>
        new(session, new StubUserRepository(session.User), new StubRoleRepository());

    private sealed class StubRoleRepository : IRoleRepository
    {
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Role?>(new Role("Cashier"));
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Role?>(null);
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>([]);
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(true);
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task AddAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }
}
