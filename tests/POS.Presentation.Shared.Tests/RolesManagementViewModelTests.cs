using POS.Application.Interfaces;
using POS.Application.UseCases.Roles;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class RolesManagementViewModelTests
{
    [Fact]
    public async Task SelectedRole_LoadsGrantsAndSavePersistsModifiedSet()
    {
        var fixture = new Fixture(new HashSet<string>(PermissionKeys.All));
        var customRole = new Role("Report Viewer");
        fixture.Roles.Add(customRole, new HashSet<string> { PermissionKeys.Reports.View });
        var viewModel = fixture.CreateViewModel();

        await viewModel.LoadAsync(TestContext.Current.CancellationToken);
        viewModel.SelectedRole = viewModel.Roles.Single(role => role.Id == customRole.Id);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.True(viewModel.PermissionOptions.Single(option => option.Key == PermissionKeys.Reports.View).IsGranted);
        viewModel.PermissionOptions.Single(option => option.Key == PermissionKeys.Users.View).IsGranted = true;
        viewModel.SaveRoleCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        IReadOnlySet<string> saved = await fixture.Roles.GetPermissionKeysAsync(customRole.Id, TestContext.Current.CancellationToken);
        Assert.True(saved.SetEquals(new HashSet<string> { PermissionKeys.Reports.View, PermissionKeys.Users.View }));
    }

    [Fact]
    public async Task SystemRoles_DisableAdministratorGrantTogglesAndAllDeactivation()
    {
        var fixture = new Fixture(new HashSet<string>(PermissionKeys.All));
        var viewModel = fixture.CreateViewModel();
        await viewModel.LoadAsync(TestContext.Current.CancellationToken);

        viewModel.SelectedRole = viewModel.Roles.Single(role => role.Id == RoleConstants.AdministratorRoleId);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.All(viewModel.PermissionOptions, option => Assert.False(option.IsEnabled));
        Assert.False(viewModel.DeactivateRoleCommand.CanExecute(null));

        viewModel.SelectedRole = viewModel.Roles.Single(role => role.Id == RoleConstants.CashierRoleId);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.False(viewModel.DeactivateRoleCommand.CanExecute(null));
        Assert.Null(typeof(RolesManagementViewModel).GetProperty("DeleteRoleCommand"));
    }

    [Fact]
    public void RolesViewOnlySession_CannotCreateSaveOrDeactivate()
    {
        var fixture = new Fixture(new HashSet<string> { PermissionKeys.Roles.View });
        var viewModel = fixture.CreateViewModel();

        Assert.True(viewModel.LoadRolesCommand.CanExecute(null));
        Assert.False(viewModel.CreateRoleCommand.CanExecute(null));
        Assert.False(viewModel.SaveRoleCommand.CanExecute(null));
        Assert.False(viewModel.DeactivateRoleCommand.CanExecute(null));
    }

    private sealed class Fixture(IReadOnlySet<string> permissions)
    {
        public FakeUsers Users { get; } = new();
        public FakeRoles Roles { get; } = new();
        public FakeSession Session { get; } = new(permissions);

        public RolesManagementViewModel CreateViewModel()
        {
            var administrator = Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName);
            var cashier = Role.CreateSystemRole(RoleConstants.CashierRoleId, RoleConstants.CashierRoleName);
            Roles.Add(administrator);
            Roles.Add(cashier);
            var user = new User("admin", "hash", administrator.Id);
            Users.Add(user);
            Session.OperatorId = user.Id;

            return new RolesManagementViewModel(
                new GetAllRolesUseCase(Users, Roles),
                new CreateRoleUseCase(Users, Roles, new NoOpUnitOfWork()),
                new UpdateRoleUseCase(Users, Roles, new NoOpUnitOfWork()),
                new DeactivateRoleUseCase(Users, Roles, new NoOpUnitOfWork()),
                Roles,
                Session);
        }
    }

    private sealed class FakeSession(IReadOnlySet<string> permissions) : ICurrentOperatorSession
    {
        public Guid OperatorId { get; set; }
        public string Username => "admin";
        public Guid RoleId => RoleConstants.AdministratorRoleId;
        public string RoleName => RoleConstants.AdministratorRoleName;
        public IReadOnlySet<string> Permissions => permissions;
        public bool IsAuthenticated => true;
        public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
    }

    private sealed class FakeUsers : IUserRepository
    {
        private readonly Dictionary<Guid, User> _users = [];
        public void Add(User user) => _users[user.Id] = user;
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_users.GetValueOrDefault(id));
        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult(_users.Values.SingleOrDefault(user => user.Username == username));
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_users.Count > 0);
        public Task AddAsync(User user, CancellationToken cancellationToken = default) { Add(user); return Task.CompletedTask; }
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>(_users.Values.ToList());
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(_users.Values.Count(user => user.IsActive && user.RoleId == roleId));
    }

    private sealed class FakeRoles : IRoleRepository
    {
        private readonly Dictionary<Guid, Role> _roles = [];
        private readonly Dictionary<Guid, IReadOnlySet<string>> _grants = [];
        public void Add(Role role, IReadOnlySet<string>? grants = null)
        {
            _roles[role.Id] = role;
            _grants[role.Id] = grants ?? new HashSet<string>(StringComparer.Ordinal);
        }

        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_roles.GetValueOrDefault(id));
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(_roles.Values.SingleOrDefault(role => role.Name == name));
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>(_roles.Values.ToList());
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(_grants.GetValueOrDefault(roleId, new HashSet<string>()).Contains(permissionKey));
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(_grants.GetValueOrDefault(roleId, new HashSet<string>(StringComparer.Ordinal)));
        public Task AddAsync(Role role, CancellationToken ct = default) { Add(role); return Task.CompletedTask; }
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default)
        {
            _grants[roleId] = new HashSet<string>(keys, StringComparer.Ordinal);
            return Task.CompletedTask;
        }

        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }
}
