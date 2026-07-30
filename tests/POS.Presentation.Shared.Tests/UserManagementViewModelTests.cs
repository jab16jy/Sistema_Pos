using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
using POS.Application.UseCases.Roles;
using POS.Domain.Entities;
using POS.Domain.Authorization;
using POS.Domain.Interfaces;
using POS.Presentation.Shared.ViewModels;
using Xunit;

namespace POS.Presentation.Shared.Tests;

public sealed class UserManagementViewModelTests
{
    [Fact]
    public async Task LoadUsersCommand_WhenRepositoryFails_SetsRecoverableErrorState()
    {
        var repository = new ThrowingUserRepository();
        var session = new FakeSession();
        var viewModel = new UserManagementViewModel(
            new GetAllUsersUseCase(repository, session),
            new CreateUserUseCase(repository, new ThrowingRoleRepository(), new FakePasswordHashService(), new NoOpUnitOfWork()),
            new UpdateUserUseCase(repository, new ThrowingRoleRepository(), new FakePasswordHashService(), new NoOpUnitOfWork()),
            new DeactivateUserUseCase(repository, new ThrowingRoleRepository(), new NoOpUnitOfWork()),
            new GetAllRolesUseCase(repository, new ThrowingRoleRepository()),
            session);

        viewModel.LoadUsersCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasError);
        Assert.Contains("Error al cargar usuarios", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateUserCommand_WithoutUsersCreatePermission_IsRejectedBeforeTouchingRepository()
    {
        var repository = new ThrowingUserRepository();
        var session = new FakeSession();
        var viewModel = new UserManagementViewModel(
            new GetAllUsersUseCase(repository, session),
            new CreateUserUseCase(repository, new ThrowingRoleRepository(), new FakePasswordHashService(), new NoOpUnitOfWork()),
            new UpdateUserUseCase(repository, new ThrowingRoleRepository(), new FakePasswordHashService(), new NoOpUnitOfWork()),
            new DeactivateUserUseCase(repository, new ThrowingRoleRepository(), new NoOpUnitOfWork()),
            new GetAllRolesUseCase(repository, new ThrowingRoleRepository()),
            session)
        {
            NewUsername = "nuevo",
            NewPassword = "1234"
        };

        viewModel.CreateUserCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasError);
        Assert.Contains("permiso", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Commands_FollowIndividualUserPermissions()
    {
        var session = new FakeSession(new HashSet<string> { PermissionKeys.Users.Create });
        var repository = new ThrowingUserRepository();
        var roles = new ThrowingRoleRepository();
        var viewModel = new UserManagementViewModel(
            new GetAllUsersUseCase(repository, session),
            new CreateUserUseCase(repository, roles, new FakePasswordHashService(), new NoOpUnitOfWork()),
            new UpdateUserUseCase(repository, roles, new FakePasswordHashService(), new NoOpUnitOfWork()),
            new DeactivateUserUseCase(repository, roles, new NoOpUnitOfWork()),
            new GetAllRolesUseCase(repository, roles),
            session);

        Assert.True(viewModel.CreateUserCommand.CanExecute(null));
        Assert.False(viewModel.SaveEditCommand.CanExecute(null));
        Assert.False(viewModel.DeactivateCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadUsersCommand_LoadsActiveRoleSummariesFromGetAllRoles()
    {
        var session = new FakeSession(new HashSet<string>(PermissionKeys.All));
        var users = new InMemoryUserRepository();
        var roles = new InMemoryRoleRepository();
        var administrator = Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName);
        var cashier = Role.CreateSystemRole(RoleConstants.CashierRoleId, RoleConstants.CashierRoleName);
        var inactive = new Role("Inactive");
        inactive.Deactivate();
        roles.Add(administrator);
        roles.Add(cashier);
        roles.Add(inactive);
        var operatorUser = new User("operator", "hash", administrator.Id);
        session.OperatorId = operatorUser.Id;
        users.Add(operatorUser);
        users.Add(new User("cashier", "hash", cashier.Id));

        var viewModel = new UserManagementViewModel(
            new GetAllUsersUseCase(users, session),
            new CreateUserUseCase(users, roles, new FakePasswordHashService(), new NoOpUnitOfWork()),
            new UpdateUserUseCase(users, roles, new FakePasswordHashService(), new NoOpUnitOfWork()),
            new DeactivateUserUseCase(users, roles, new NoOpUnitOfWork()),
            new GetAllRolesUseCase(users, roles),
            session);

        viewModel.LoadUsersCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal([administrator.Id, cashier.Id], viewModel.AvailableRoles.Select(role => role.Id));
        Assert.DoesNotContain(viewModel.AvailableRoles, role => role.Id == inactive.Id);
    }

    private sealed class ThrowingUserRepository : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falló usuarios");

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falló usuarios");

        public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("falló usuarios");

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(User user, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default)
            => Task.FromResult(0);
    }

    private sealed class FakePasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password) => $"hash:{password}";

        public bool VerifyPassword(string password, string passwordHash) => passwordHash == $"hash:{password}";
    }

    private sealed class ThrowingRoleRepository : IRoleRepository
    {
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new InvalidOperationException("falló roles");
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => throw new InvalidOperationException("falló roles");
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => throw new InvalidOperationException("falló roles");
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task AddAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class FakeSession : ICurrentOperatorSession
    {
        public FakeSession(IReadOnlySet<string>? permissions = null)
        {
            OperatorId = Guid.NewGuid();
            Permissions = permissions ?? new HashSet<string>();
        }

        public Guid OperatorId { get; set; }
        public string Username => "admin";
        public Guid RoleId => RoleConstants.AdministratorRoleId;
        public string RoleName => RoleConstants.AdministratorRoleName;
        public IReadOnlySet<string> Permissions { get; }
        public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
        public bool IsAuthenticated => true;
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        private readonly Dictionary<Guid, User> _users = [];
        public void Add(User user) => _users[user.Id] = user;
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_users.GetValueOrDefault(id));
        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult(_users.Values.SingleOrDefault(user => user.Username == username));
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(_users.Count > 0);
        public Task AddAsync(User user, CancellationToken cancellationToken = default) { Add(user); return Task.CompletedTask; }
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>(_users.Values.ToList());
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(User user, CancellationToken cancellationToken = default) { _users.Remove(user.Id); return Task.CompletedTask; }
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(_users.Values.Count(user => user.RoleId == roleId && user.IsActive));
    }

    private sealed class InMemoryRoleRepository : IRoleRepository
    {
        private readonly Dictionary<Guid, Role> _roles = [];
        public void Add(Role role) => _roles[role.Id] = role;
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_roles.GetValueOrDefault(id));
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult(_roles.Values.SingleOrDefault(role => role.Name == name));
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>(_roles.Values.ToList());
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(false);
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task AddAsync(Role role, CancellationToken ct = default) { Add(role); return Task.CompletedTask; }
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }
}
