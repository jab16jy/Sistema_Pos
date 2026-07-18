using POS.Application.DTOs.Shifts;
using POS.Application.Services;
using POS.Application.UseCases.Shifts;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Application.Tests;

public sealed class ShiftClosingSummaryTests
{
    [Fact]
    public async Task CloseShift_PersistsFreshBoundedSalesSummary()
    {
        var (user, authorizer) = CreateAuthorizedAuthorizer();
        var shift = new CashierShift(user.Id, user.Username, new Money(100m));
        var shifts = new SingleShiftRepository(shift);
        var sales = new BoundedSummarySaleRepository(new ShiftSalesSummaryData(4_200m, 2_800m, 1_400m));

        var result = await new CloseShiftUseCase(shifts, sales, new NoOpUnitOfWork(), authorizer)
            .ExecuteAsync(new CloseShiftRequest(shift.Id, 2_900m, null), TestContext.Current.CancellationToken);

        Assert.True(result.HasClosingSummary);
        Assert.Equal(4_200m, result.SalesTotal);
        Assert.Equal(2_800m, result.CashPayments);
        Assert.Equal(1_400m, result.DigitalPayments);
        Assert.Equal(2_900m, result.ExpectedCash);
        Assert.NotNull(sales.ClosedAt);
        Assert.True(sales.ClosedAt > shift.OpenedAt);
    }

    [Fact]
    public async Task CloseShift_PersistsReturnOutflowsAndSubtractsOnlyCashFromExpectedDrawer()
    {
        var (user, authorizer) = CreateAuthorizedAuthorizer();
        var shift = new CashierShift(user.Id, user.Username, new Money(100m));
        var shifts = new SingleShiftRepository(shift);
        var sales = new BoundedSummarySaleRepository(new ShiftSalesSummaryData(
            SalesTotal: 4_200m,
            CashPayments: 2_800m,
            DigitalPayments: 1_400m,
            CashReturnOutflows: 600m,
            DigitalReturnOutflows: 700m));

        var result = await new CloseShiftUseCase(shifts, sales, new NoOpUnitOfWork(), authorizer)
            .ExecuteAsync(new CloseShiftRequest(shift.Id, 2_300m, null), TestContext.Current.CancellationToken);

        Assert.Equal(600m, result.CashReturnOutflows);
        Assert.Equal(700m, result.DigitalReturnOutflows);
        Assert.Equal(2_300m, result.ExpectedCash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CloseShift_RejectsAnotherCashierAndAdministratorWithoutMutating(bool administrator)
    {
        var owner = new User("owner", "hash", Guid.NewGuid());
        var actorRole = administrator
            ? Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName)
            : new Role("Cashier");
        var actor = new User("other", "hash", actorRole.Id);
        var shift = new CashierShift(owner.Id, owner.Username, new Money(100m));
        var shifts = new SingleShiftRepository(shift);
        var sales = new BoundedSummarySaleRepository(ShiftSalesSummaryData.Empty);
        var unitOfWork = new NoOpUnitOfWork();
        var authorizer = new CriticalCommandAuthorizer(
            new TestSession(actor.Id, true),
            new FakeUsers(actor),
            new FakeRoles(actorRole, true));

        await Assert.ThrowsAsync<UnauthorizedActionException>(() => new CloseShiftUseCase(shifts, sales, unitOfWork, authorizer)
            .ExecuteAsync(new CloseShiftRequest(shift.Id, 100m, null), TestContext.Current.CancellationToken));

        Assert.Equal(0, sales.SummaryCalls);
        Assert.Equal(0, unitOfWork.SaveCalls);
        Assert.Equal(POS.Domain.Enums.ShiftStatus.Open, shift.Status);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task CloseShift_RejectsInvalidAuthorizationBeforeReadingShift(bool authenticated, bool active, bool permitted)
    {
        var user = new User("cashier", "hash", Guid.NewGuid());
        if (!active)
        {
            user.Deactivate();
        }

        var shift = new CashierShift(user.Id, user.Username, new Money(100m));
        var shifts = new SingleShiftRepository(shift);
        var sales = new BoundedSummarySaleRepository(ShiftSalesSummaryData.Empty);
        var authorizer = new CriticalCommandAuthorizer(
            new TestSession(user.Id, authenticated),
            new FakeUsers(user),
            new FakeRoles(new Role("Cashier"), permitted));

        await Assert.ThrowsAsync<UnauthorizedActionException>(() => new CloseShiftUseCase(shifts, sales, new NoOpUnitOfWork(), authorizer)
            .ExecuteAsync(new CloseShiftRequest(shift.Id, 100m, null), TestContext.Current.CancellationToken));

        Assert.Equal(0, shifts.GetByIdCalls);
        Assert.Equal(0, sales.SummaryCalls);
    }

    private static (User User, CriticalCommandAuthorizer Authorizer) CreateAuthorizedAuthorizer()
    {
        var user = new User("cashier", "hash", Guid.NewGuid());
        return (user, new CriticalCommandAuthorizer(new TestSession(user.Id, true), new FakeUsers(user), new FakeRoles(new Role("Cashier"), true)));
    }

    private sealed class SingleShiftRepository(CashierShift shift) : ICashierShiftRepository
    {
        public int GetByIdCalls { get; private set; }
        public Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
            => Task.FromResult<CashierShift?>(shift);

        public Task<CashierShift?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetByIdCalls++;
            return Task.FromResult<CashierShift?>(id == shift.Id ? shift : null);
        }

        public Task<IReadOnlyList<CashierShift>> GetRecentAsync(int limit = 50, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CashierShift>>([shift]);

        public Task AddAsync(CashierShift cashierShift, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class BoundedSummarySaleRepository(ShiftSalesSummaryData summary) : ISaleRepository
    {
        public DateTimeOffset? ClosedAt { get; private set; }
        public int SummaryCalls { get; private set; }

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
            DateTimeOffset closedAt,
            CancellationToken cancellationToken = default)
        {
            SummaryCalls++;
            ClosedAt = closedAt;
            return Task.FromResult(summary);
        }
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class TestSession(Guid operatorId, bool isAuthenticated) : POS.Application.Interfaces.ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = operatorId;
        public string Username => string.Empty;
        public bool IsAuthenticated { get; } = isAuthenticated;
    }

    private sealed class FakeUsers(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<User?>(id == user.Id ? user : null);
        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>([]);
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeRoles(Role role, bool permitted) : IRoleRepository
    {
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Role?>(role);
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Role?>(null);
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>([]);
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(permitted);
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task AddAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }
}
