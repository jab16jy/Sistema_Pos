using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
using POS.Domain.Entities;
using POS.Domain.Enums;
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
        var viewModel = new UserManagementViewModel(
            new GetAllUsersUseCase(repository),
            new CreateUserUseCase(repository, new FakePasswordHashService(), new NoOpUnitOfWork()),
            new UpdateUserUseCase(repository, new FakePasswordHashService()),
            new DeactivateUserUseCase(repository),
            new FakeSession());

        viewModel.LoadUsersCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasError);
        Assert.Contains("Error al cargar usuarios", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateUserCommand_WhenCurrentUserIsCashier_DoesNotCreateUserAndShowsPermissionError()
    {
        var repository = new InMemoryUserRepository();
        var viewModel = new UserManagementViewModel(
            new GetAllUsersUseCase(repository),
            new CreateUserUseCase(repository, new FakePasswordHashService(), new NoOpUnitOfWork()),
            new UpdateUserUseCase(repository, new FakePasswordHashService()),
            new DeactivateUserUseCase(repository),
            new FakeSession(UserRole.Cashier))
        {
            NewUsername = "nuevo",
            NewPassword = "1234",
            NewRole = UserRole.Administrator
        };

        viewModel.CreateUserCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Empty(repository.Users);
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
    }

    private sealed class InMemoryUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(user => user.Id == id));

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
            => Task.FromResult(Users.FirstOrDefault(user => user.Username == username && user.IsActive));

        public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Users.Count > 0);

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<User>>(Users);

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
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

    private sealed class FakeSession : ICurrentOperatorSession
    {
        private readonly UserRole _role;

        public FakeSession(UserRole role = UserRole.Administrator) => _role = role;

        public Guid OperatorId => Guid.NewGuid();
        public string Username => "admin";
        public UserRole Role => _role;
        public bool IsAuthenticated => true;
    }
}
