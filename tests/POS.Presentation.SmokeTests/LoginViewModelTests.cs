using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Presentation.ViewModels;
using Xunit;

namespace POS.Presentation.SmokeTests;

public sealed class LoginViewModelTests
{
    [Fact]
    public async Task Login_WithEmptyUsername_SetsValidationMessageAndDoesNotInvokeUseCase()
    {
        var userRepository = new FakeUserRepository();
        var hashService = new FakePasswordHashService();
        var auditLogRepository = new FakeAuditLogRepository();
        var unitOfWork = new FakeUnitOfWork();
        var loginUseCase = new LoginUseCase(userRepository, hashService, auditLogRepository, unitOfWork);
        var session = new FakeWritableSession();
        var viewModel = new LoginViewModel(loginUseCase, session)
        {
            Username = "",
            Password = "somepassword"
        };

        await viewModel.ExecuteLoginAsync();

        Assert.Equal("El usuario es requerido.", viewModel.Message);
        Assert.False(userRepository.GetByUsernameInvoked);
    }

    [Fact]
    public async Task Login_WithEmptyPassword_SetsValidationMessageAndDoesNotInvokeUseCase()
    {
        var userRepository = new FakeUserRepository();
        var hashService = new FakePasswordHashService();
        var auditLogRepository = new FakeAuditLogRepository();
        var unitOfWork = new FakeUnitOfWork();
        var loginUseCase = new LoginUseCase(userRepository, hashService, auditLogRepository, unitOfWork);
        var session = new FakeWritableSession();
        var viewModel = new LoginViewModel(loginUseCase, session)
        {
            Username = "someuser",
            Password = ""
        };

        await viewModel.ExecuteLoginAsync();

        Assert.Equal("La contraseña es requerida.", viewModel.Message);
        Assert.False(userRepository.GetByUsernameInvoked);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_SetsServiceReturnedErrorMessage()
    {
        var userRepository = new FakeUserRepository();
        var hashService = new FakePasswordHashService();
        var auditLogRepository = new FakeAuditLogRepository();
        var unitOfWork = new FakeUnitOfWork();
        var loginUseCase = new LoginUseCase(userRepository, hashService, auditLogRepository, unitOfWork);
        var session = new FakeWritableSession();
        var viewModel = new LoginViewModel(loginUseCase, session)
        {
            Username = "nonexistent",
            Password = "password"
        };

        await viewModel.ExecuteLoginAsync();

        Assert.True(userRepository.GetByUsernameInvoked);
        Assert.NotEmpty(viewModel.Message);
        Assert.NotEqual("El usuario es requerido.", viewModel.Message);
        Assert.NotEqual("La contraseña es requerida.", viewModel.Message);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public bool GetByUsernameInvoked { get; private set; }

        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            GetByUsernameInvoked = true;
            return Task.FromResult<User?>(null);
        }

        public Task<bool> AnyAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<User>>([]);

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(null);

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakePasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password) => $"hash:{password}";

        public bool VerifyPassword(string password, string passwordHash) => passwordHash == $"hash:{password}";
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public Task<IReadOnlyList<AuditLog>> GetRecentAsync(
            int limit = 100,
            AuditLogFilter? filter = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AuditLog>>([]);

        public Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(0);
    }

    private sealed class FakeWritableSession : IWritableOperatorSession
    {
        public Guid OperatorId { get; private set; }
        public string Username { get; private set; } = string.Empty;
        public UserRole Role { get; private set; }
        public bool IsAuthenticated { get; private set; }

        public void SetUser(Guid operatorId, string username, UserRole role)
        {
            OperatorId = operatorId;
            Username = username;
            Role = role;
            IsAuthenticated = true;
        }

        public void ClearSession()
        {
            IsAuthenticated = false;
        }
    }
}
