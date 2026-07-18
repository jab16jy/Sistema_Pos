using POS.Application.Interfaces;
using POS.Application.Services;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using Xunit;

namespace POS.Application.Tests.Auth;

public sealed class CriticalCommandAuthorizerTests
{
    [Fact]
    public async Task AuthorizeAsync_RejectsUnauthenticatedSessionBeforeReadingRepositories()
    {
        var users = new FakeUsers();
        var roles = new FakeRoles();
        var authorizer = new CriticalCommandAuthorizer(new FakeSession(), users, roles);

        await Assert.ThrowsAsync<UnauthorizedActionException>(() => authorizer.AuthorizeAsync(PermissionKeys.Cashier.Sell, TestContext.Current.CancellationToken));

        Assert.Equal(0, users.GetByIdCalls);
        Assert.Equal(0, roles.PermissionChecks);
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsMissingOrInactivePersistedUserBeforePermissionCheck()
    {
        var session = new FakeSession(Guid.NewGuid());
        var missingUsers = new FakeUsers();
        var missingRoles = new FakeRoles();

        await Assert.ThrowsAsync<UnauthorizedActionException>(() => new CriticalCommandAuthorizer(session, missingUsers, missingRoles).AuthorizeAsync(PermissionKeys.Cashier.Sell, TestContext.Current.CancellationToken));

        User inactive = new("inactive", "hash", Guid.NewGuid());
        inactive.Deactivate();
        var inactiveRoles = new FakeRoles();
        await Assert.ThrowsAsync<UnauthorizedActionException>(() => new CriticalCommandAuthorizer(new FakeSession(inactive.Id), new FakeUsers(inactive), inactiveRoles).AuthorizeAsync(PermissionKeys.Cashier.Sell, TestContext.Current.CancellationToken));

        Assert.Equal(0, missingRoles.PermissionChecks);
        Assert.Equal(0, inactiveRoles.PermissionChecks);
    }

    [Fact]
    public async Task AuthorizeAsync_RejectsUserWithoutPersistedPermission()
    {
        User user = new("cashier", "hash", Guid.NewGuid());
        var roles = new FakeRoles(hasPermission: false);
        var authorizer = new CriticalCommandAuthorizer(new FakeSession(user.Id), new FakeUsers(user), roles);

        await Assert.ThrowsAsync<UnauthorizedActionException>(() => authorizer.AuthorizeAsync(PermissionKeys.Cashier.Sell, TestContext.Current.CancellationToken));

        Assert.Equal(1, roles.PermissionChecks);
    }

    [Fact]
    public async Task AuthorizeAsync_ReturnsPersistedActorForAuthorizedUser()
    {
        User user = new("cashier", "hash", Guid.NewGuid());
        var authorizer = new CriticalCommandAuthorizer(new FakeSession(user.Id), new FakeUsers(user), new FakeRoles(hasPermission: true));

        CanonicalActor actor = await authorizer.AuthorizeAsync(PermissionKeys.Cashier.Sell, TestContext.Current.CancellationToken);

        Assert.Equal(user.Id, actor.UserId);
        Assert.Equal(user.Username, actor.Username);
    }

    private sealed class FakeSession(Guid operatorId = default) : ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = operatorId;
        public string Username => string.Empty;
        public bool IsAuthenticated => OperatorId != Guid.Empty;
    }

    private sealed class FakeUsers(User? user = null) : IUserRepository
    {
        public int GetByIdCalls { get; private set; }
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            GetByIdCalls++;
            return Task.FromResult(user?.Id == id ? user : null);
        }

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>([]);
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakeRoles(bool hasPermission = false) : IRoleRepository
    {
        public int PermissionChecks { get; private set; }
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Role?>(new Role("Cashier"));
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Role?>(null);
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>([]);
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default)
        {
            PermissionChecks++;
            return Task.FromResult(hasPermission);
        }

        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task AddAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }
}
