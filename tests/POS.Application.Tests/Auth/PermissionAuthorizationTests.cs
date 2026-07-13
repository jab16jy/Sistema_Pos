using POS.Application.Interfaces;
using POS.Application.UseCases.Audit;
using POS.Application.UseCases.Auth;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests.Auth;

public sealed class PermissionAuthorizationTests
{
    [Fact]
    public async Task RepositoryGate_UsesAdministratorBypassAndHonorsRoleGrants()
    {
        var users = new FakeUsers();
        var roles = new FakeRoles();
        var administrator = new User("admin", "hash", RoleConstants.AdministratorRoleId);
        var customRole = new Role("Report Viewer");
        var viewer = new User("viewer", "hash", customRole.Id);
        users.Add(administrator);
        users.Add(viewer);
        roles.Add(Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName));
        roles.Add(customRole, new HashSet<string> { PermissionKeys.Reports.View });

        Assert.Null(await PermissionAuthorization.EnsureHasPermissionAsync(users, roles, administrator.Id, PermissionKeys.Users.Create, "denied", TestContext.Current.CancellationToken));
        Assert.Equal(0, roles.HasPermissionCalls);
        Assert.Null(await PermissionAuthorization.EnsureHasPermissionAsync(users, roles, viewer.Id, PermissionKeys.Reports.View, "denied", TestContext.Current.CancellationToken));
        Assert.Equal("denied", await PermissionAuthorization.EnsureHasPermissionAsync(users, roles, viewer.Id, PermissionKeys.Users.Create, "denied", TestContext.Current.CancellationToken));
        Assert.Equal("denied", await PermissionAuthorization.EnsureHasPermissionAsync(users, roles, Guid.Empty, PermissionKeys.Users.Create, "denied", TestContext.Current.CancellationToken));

        viewer.Deactivate();
        Assert.Equal("denied", await PermissionAuthorization.EnsureHasPermissionAsync(users, roles, viewer.Id, PermissionKeys.Reports.View, "denied", TestContext.Current.CancellationToken));

        var inactiveRole = new Role("Inactive");
        inactiveRole.Deactivate();
        var inactiveRoleUser = new User("inactive-role", "hash", inactiveRole.Id);
        users.Add(inactiveRoleUser);
        roles.Add(inactiveRole, new HashSet<string> { PermissionKeys.Reports.View });
        Assert.Equal("denied", await PermissionAuthorization.EnsureHasPermissionAsync(users, roles, inactiveRoleUser.Id, PermissionKeys.Reports.View, "denied", TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SessionGate_RequiresAnAuthenticatedSnapshotGrant()
    {
        var session = new FakeSession(isAuthenticated: true, new HashSet<string> { PermissionKeys.Settings.View });
        Assert.Null(PermissionAuthorization.EnsureHasPermission(session, PermissionKeys.Settings.View, "denied"));
        Assert.Equal("denied", PermissionAuthorization.EnsureHasPermission(session, PermissionKeys.Settings.Edit, "denied"));
        Assert.Equal("denied", PermissionAuthorization.EnsureHasPermission(new FakeSession(false, new HashSet<string> { PermissionKeys.Settings.View }), PermissionKeys.Settings.View, "denied"));
    }

    [Fact]
    public void InMemorySession_IsEmptyWhenUnauthenticatedAndUsesAnImmutableCopyOfTheSnapshot()
    {
        var session = new InMemoryUserSession();
        Assert.Empty(session.Permissions);
        Assert.False(session.HasPermission(PermissionKeys.Reports.View));

        var grants = new HashSet<string> { PermissionKeys.Reports.View };
        session.SetUser(Guid.NewGuid(), "viewer", Guid.NewGuid(), "Report Viewer", grants);
        grants.Add(PermissionKeys.Users.View);
        Assert.True(session.HasPermission(PermissionKeys.Reports.View));
        Assert.False(session.HasPermission(PermissionKeys.Users.View));

        session.ClearSession();
        Assert.Empty(session.Permissions);
    }

    [Fact]
    public async Task GetAllUsers_DeniesWithoutUsersViewBeforeReadingRepository()
    {
        var users = new FakeUsers();
        users.Add(new User("hidden", "hash", RoleConstants.CashierRoleId));
        var session = new FakeSession(true, new HashSet<string>());

        await Assert.ThrowsAsync<POS.Domain.Exceptions.UnauthorizedActionException>(() =>
            new GetAllUsersUseCase(users, session).ExecuteAsync(TestContext.Current.CancellationToken));

        Assert.False(users.GetAllInvoked);
    }

    [Fact]
    public async Task GetAllUsers_AllowsSessionWithUsersView()
    {
        var users = new FakeUsers();
        users.Add(new User("viewer", "hash", RoleConstants.CashierRoleId));
        var session = new FakeSession(true, new HashSet<string> { PermissionKeys.Users.View });

        IReadOnlyList<UserSummary> result = await new GetAllUsersUseCase(users, session).ExecuteAsync(TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.True(users.GetAllInvoked);
    }

    [Fact]
    public async Task UserManagementUseCases_AllowCustomRolesWithTheirSpecificPermissions()
    {
        var users = new FakeUsers();
        var roles = new FakeRoles();
        var createRole = new Role("User Creator");
        var editRole = new Role("User Editor");
        var deactivateRole = new Role("User Deactivator");
        roles.Add(createRole, new HashSet<string> { PermissionKeys.Users.Create });
        roles.Add(editRole, new HashSet<string> { PermissionKeys.Users.Edit });
        roles.Add(deactivateRole, new HashSet<string> { PermissionKeys.Users.Deactivate });
        var creator = new User("creator", "hash", createRole.Id);
        var editor = new User("editor", "hash", editRole.Id);
        var deactivator = new User("deactivator", "hash", deactivateRole.Id);
        var target = new User("target", "hash", RoleConstants.CashierRoleId);
        users.Add(creator);
        users.Add(editor);
        users.Add(deactivator);
        users.Add(target);

        var create = await new CreateUserUseCase(users, roles, new FakePasswordHashService(), new NoOpUnitOfWork())
            .ExecuteAsync("new-user", "1234", RoleConstants.CashierRoleId, creator.Id, TestContext.Current.CancellationToken);
        var edit = await new UpdateUserUseCase(users, roles, new FakePasswordHashService())
            .ExecuteAsync(target.Id, "5678", RoleConstants.CashierRoleId, editor.Id, TestContext.Current.CancellationToken);
        var deactivate = await new DeactivateUserUseCase(users, roles)
            .ExecuteAsync(target.Id, deactivator.Id, TestContext.Current.CancellationToken);

        Assert.True(create.Success, create.Error);
        Assert.True(edit.Success, edit.Error);
        Assert.True(deactivate.Success, deactivate.Error);
        Assert.Equal(RoleConstants.CashierRoleId, target.RoleId);
        Assert.False(target.IsActive);
    }

    [Fact]
    public async Task GetAuditLogs_AllowCustomRoleWithAuditView()
    {
        var users = new FakeUsers();
        var roles = new FakeRoles();
        var auditRole = new Role("Auditor");
        var auditor = new User("auditor", "hash", auditRole.Id);
        users.Add(auditor);
        roles.Add(auditRole, new HashSet<string> { PermissionKeys.Audit.View });

        IReadOnlyList<POS.Application.DTOs.Audit.AuditLogEntry> logs = await new GetAuditLogsUseCase(new EmptyAuditLogRepository(), users, roles)
            .ExecuteAsync(auditor.Id, ct: TestContext.Current.CancellationToken);

        Assert.Empty(logs);
    }

    [Fact]
    public async Task CreateUser_CustomCreatorCannotAssignAdministratorButAdministratorCan()
    {
        var users = new FakeUsers();
        var roles = new FakeRoles();
        var creatorRole = new Role("Creator");
        var creator = new User("creator", "hash", creatorRole.Id);
        var administrator = new User("administrator", "hash", RoleConstants.AdministratorRoleId);
        users.Add(creator);
        users.Add(administrator);
        roles.Add(creatorRole, new HashSet<string> { PermissionKeys.Users.Create });
        roles.Add(Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName));
        var useCase = new CreateUserUseCase(users, roles, new FakePasswordHashService(), new NoOpUnitOfWork());

        var denied = await useCase.ExecuteAsync("blocked-admin", "1234", RoleConstants.AdministratorRoleId, creator.Id, TestContext.Current.CancellationToken);
        var allowed = await useCase.ExecuteAsync("allowed-admin", "1234", RoleConstants.AdministratorRoleId, administrator.Id, TestContext.Current.CancellationToken);

        Assert.False(denied.Success);
        Assert.Contains("Administrador", denied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await users.GetByUsernameAsync("blocked-admin", TestContext.Current.CancellationToken));
        Assert.True(allowed.Success, allowed.Error);
    }

    [Fact]
    public async Task UpdateUser_CustomEditorCannotPromoteSelfOrMutateAdministratorButAdministratorCan()
    {
        var users = new FakeUsers();
        var roles = new FakeRoles();
        var editorRole = new Role("Editor");
        var editor = new User("editor", "hash:original", editorRole.Id);
        var administrator = new User("administrator", "hash:admin", RoleConstants.AdministratorRoleId);
        users.Add(editor);
        users.Add(administrator);
        roles.Add(editorRole, new HashSet<string> { PermissionKeys.Users.Edit });
        roles.Add(Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName));
        var useCase = new UpdateUserUseCase(users, roles, new FakePasswordHashService());

        var selfPromotion = await useCase.ExecuteAsync(editor.Id, null, RoleConstants.AdministratorRoleId, editor.Id, TestContext.Current.CancellationToken);
        var mutateAdministrator = await useCase.ExecuteAsync(administrator.Id, "5678", RoleConstants.AdministratorRoleId, editor.Id, TestContext.Current.CancellationToken);

        Assert.False(selfPromotion.Success);
        Assert.False(mutateAdministrator.Success);
        Assert.Equal(editorRole.Id, editor.RoleId);
        Assert.Equal("hash:admin", administrator.PasswordHash);

        var administratorSuccess = await useCase.ExecuteAsync(administrator.Id, "5678", RoleConstants.AdministratorRoleId, administrator.Id, TestContext.Current.CancellationToken);

        Assert.True(administratorSuccess.Success, administratorSuccess.Error);
        Assert.Equal("hash:5678", administrator.PasswordHash);
    }

    private sealed class FakeSession(bool isAuthenticated, IReadOnlySet<string> permissions) : ICurrentOperatorSession
    {
        public Guid OperatorId => Guid.NewGuid();
        public string Username => "operator";
        public Guid RoleId => Guid.NewGuid();
        public string RoleName => "Custom";
        public IReadOnlySet<string> Permissions => permissions;
        public bool IsAuthenticated => isAuthenticated;
        public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
    }

    private sealed class FakeUsers : IUserRepository
    {
        private readonly Dictionary<Guid, User> _users = [];
        public bool GetAllInvoked { get; private set; }
        public void Add(User user) => _users[user.Id] = user;
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_users.GetValueOrDefault(id));
        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult(_users.Values.FirstOrDefault(user => user.Username == username));
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_users.Count > 0);
        public Task AddAsync(User user, CancellationToken cancellationToken = default) { Add(user); return Task.CompletedTask; }
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            GetAllInvoked = true;
            return Task.FromResult<IReadOnlyList<User>>(_users.Values.ToList());
        }
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(_users.Values.Count(user => user.RoleId == roleId && user.IsActive));
    }

    private sealed class FakeRoles : IRoleRepository
    {
        private readonly Dictionary<Guid, Role> _roles = [];
        private readonly Dictionary<Guid, IReadOnlySet<string>> _grants = [];
        public int HasPermissionCalls { get; private set; }
        public void Add(Role role, IReadOnlySet<string>? grants = null) { _roles[role.Id] = role; _grants[role.Id] = grants ?? new HashSet<string>(); }
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_roles.GetValueOrDefault(id));
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(_roles.Values.FirstOrDefault(role => role.Name == name));
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>(_roles.Values.ToList());
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) { HasPermissionCalls++; return Task.FromResult(_grants.GetValueOrDefault(roleId, new HashSet<string>()).Contains(permissionKey)); }
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(_grants.GetValueOrDefault(roleId, new HashSet<string>()));
        public Task AddAsync(Role role, CancellationToken ct = default) { Add(role); return Task.CompletedTask; }
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) { _grants[roleId] = new HashSet<string>(keys); return Task.CompletedTask; }
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class FakePasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password) => $"hash:{password}";
        public bool VerifyPassword(string password, string passwordHash) => passwordHash == $"hash:{password}";
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class EmptyAuditLogRepository : IAuditLogRepository
    {
        public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(int limit = 100, POS.Domain.ValueObjects.AuditLogFilter? filter = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AuditLog>>([]);
    }
}
