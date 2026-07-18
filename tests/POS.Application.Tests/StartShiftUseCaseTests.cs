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

public sealed class StartShiftUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WithRequestedTimestamp_PreservesItNormalizedAsUtc()
    {
        var repository = new CapturingShiftRepository();
        var (user, authorizer) = CreateAuthorizedAuthorizer();
        var useCase = new StartShiftUseCase(repository, new NoOpUnitOfWork(), authorizer);
        var requestedAt = DateTimeOffset.UtcNow.AddHours(-2).ToOffset(TimeSpan.FromHours(-5));

        var result = await useCase.ExecuteAsync(new StartShiftRequest(100m, null, requestedAt), TestContext.Current.CancellationToken);

        Assert.NotNull(repository.AddedShift);
        Assert.Equal(requestedAt.ToUniversalTime(), repository.AddedShift!.OpenedAt);
        Assert.Equal(requestedAt.ToUniversalTime(), result.OpenedAt);
        Assert.Equal(TimeSpan.Zero, repository.AddedShift.OpenedAt.Offset);
        Assert.Equal(user.Id, repository.AddedShift.CashierUserId);
        Assert.Equal(user.Username, repository.AddedShift.CashierUsername);
    }

    [Fact]
    public async Task ExecuteAsync_WithFutureTimestamp_RejectsItUsingUtcComparison()
    {
        var repository = new CapturingShiftRepository();
        var (_, authorizer) = CreateAuthorizedAuthorizer();
        var useCase = new StartShiftUseCase(repository, new NoOpUnitOfWork(), authorizer);
        var futureAt = DateTimeOffset.UtcNow.AddMinutes(10).ToOffset(TimeSpan.FromHours(-11));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(
            new StartShiftRequest(100m, null, futureAt),
            TestContext.Current.CancellationToken));

        Assert.Contains("futuro", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(repository.AddedShift);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task ExecuteAsync_RejectsInvalidAuthorizationBeforeReadingOrStagingShift(
        bool authenticated,
        bool active,
        bool permitted)
    {
        var user = new User("cashier", "hash", Guid.NewGuid());
        if (!active)
        {
            user.Deactivate();
        }

        var repository = new CapturingShiftRepository();
        var authorizer = new CriticalCommandAuthorizer(
            new TestSession(user.Id, authenticated),
            new FakeUsers(user),
            new FakeRoles(permitted));

        await Assert.ThrowsAsync<UnauthorizedActionException>(() => new StartShiftUseCase(repository, new NoOpUnitOfWork(), authorizer)
            .ExecuteAsync(new StartShiftRequest(100m, null), TestContext.Current.CancellationToken));

        Assert.Equal(0, repository.OpenShiftCalls);
        Assert.Null(repository.AddedShift);
    }

    private static (User User, CriticalCommandAuthorizer Authorizer) CreateAuthorizedAuthorizer()
    {
        var user = new User("cashier", "hash", Guid.NewGuid());
        return (user, new CriticalCommandAuthorizer(new TestSession(user.Id, true), new FakeUsers(user), new FakeRoles(true)));
    }

    private sealed class CapturingShiftRepository : ICashierShiftRepository
    {
        public CashierShift? AddedShift { get; private set; }
        public int OpenShiftCalls { get; private set; }

        public Task<CashierShift?> GetOpenShiftAsync(Guid cashierUserId, CancellationToken cancellationToken = default)
        {
            OpenShiftCalls++;
            return Task.FromResult<CashierShift?>(null);
        }

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

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class TestSession(Guid operatorId, bool isAuthenticated) : POS.Application.Interfaces.ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = operatorId;
        public string Username => string.Empty;
        public bool IsAuthenticated { get; } = isAuthenticated;
    }

    private sealed class FakeUsers(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(id == user.Id ? user : null);
        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>([]);
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeRoles(bool permitted) : IRoleRepository
    {
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Role?>(new Role("Cashier"));
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
