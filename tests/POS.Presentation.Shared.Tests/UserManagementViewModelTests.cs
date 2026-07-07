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
            new CreateUserUseCase(repository, new FakePasswordHashService()),
            new UpdateUserUseCase(repository, new FakePasswordHashService()),
            new DeactivateUserUseCase(repository),
            new FakeSession());

        viewModel.LoadUsersCommand.Execute(null);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasError);
        Assert.Contains("Error al cargar usuarios", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateUserCommand_AsCashier_IsRejectedBeforeTouchingRepository()
    {
        // ThrowingUserRepository would throw if the guard let execution reach the repo,
        // which would surface a different ("Error al crear usuario") message.
        var repository = new ThrowingUserRepository();
        var viewModel = new UserManagementViewModel(
            new GetAllUsersUseCase(repository),
            new CreateUserUseCase(repository, new FakePasswordHashService()),
            new UpdateUserUseCase(repository, new FakePasswordHashService()),
            new DeactivateUserUseCase(repository),
            new FakeSession(UserRole.Cashier))
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
    }

    private sealed class FakePasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password) => $"hash:{password}";

        public bool VerifyPassword(string password, string passwordHash) => passwordHash == $"hash:{password}";
    }

    private sealed class FakeSession : ICurrentOperatorSession
    {
        public FakeSession(UserRole role = UserRole.Administrator) => Role = role;

        public Guid OperatorId => Guid.NewGuid();
        public string Username => "admin";
        public UserRole Role { get; }
        public bool IsAuthenticated => true;
    }
}
