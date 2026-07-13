using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
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
            new UpdateUserUseCase(repository, new ThrowingRoleRepository(), new FakePasswordHashService()),
            new DeactivateUserUseCase(repository, new ThrowingRoleRepository()),
            session);

        viewModel.LoadUsersCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasError);
        Assert.Contains("Error al cargar usuarios", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateUserCommand_AsCashier_IsRejectedBeforeTouchingRepository()
    {
        var repository = new ThrowingUserRepository();
        var session = new FakeSession(RoleConstants.CashierRoleId);
        var viewModel = new UserManagementViewModel(
            new GetAllUsersUseCase(repository, session),
            new CreateUserUseCase(repository, new ThrowingRoleRepository(), new FakePasswordHashService(), new NoOpUnitOfWork()),
            new UpdateUserUseCase(repository, new ThrowingRoleRepository(), new FakePasswordHashService()),
            new DeactivateUserUseCase(repository, new ThrowingRoleRepository()),
            session)
        {
            NewUsername = "nuevo",
            NewPassword = "1234"
        };

        viewModel.CreateUserCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasError);
        Assert.Contains("administrador", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
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
        public FakeSession(Guid roleId = default) => RoleId = roleId == Guid.Empty ? RoleConstants.AdministratorRoleId : roleId;

        public Guid OperatorId => Guid.NewGuid();
        public string Username => "admin";
        public Guid RoleId { get; }
        public string RoleName => RoleConstants.IsSystemAdministrator(RoleId) ? RoleConstants.AdministratorRoleName : RoleConstants.CashierRoleName;
        public IReadOnlySet<string> Permissions => RoleConstants.IsSystemAdministrator(RoleId) ? new HashSet<string>(PermissionKeys.All) : new HashSet<string>();
        public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
        public bool IsAuthenticated => true;
    }
}
