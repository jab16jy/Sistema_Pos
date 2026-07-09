using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Audit;
using POS.Application.DTOs.Auth;
using POS.Application.Interfaces;
using POS.Application.UseCases.Audit;
using POS.Application.UseCases.Auth;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests.Auth;

// Regression coverage for the "login/logout never audited" gap: LoginUseCase used to only read and
// verify credentials without ever calling SaveChangesAsync, so the global audit interceptor in
// AppDbContext never fired for successful logins, failed logins, or logout.
public sealed class LoginUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_OnSuccess_WritesLoginAuditEntry()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var userRepository = new UserRepository(dbContext);
        var admin = new User("admin", "hash:1234", UserRole.Administrator);
        await userRepository.AddAsync(admin, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var auditRepository = new AuditLogRepository(dbContext);
        var useCase = new LoginUseCase(userRepository, new FakePasswordHashService(), auditRepository, dbContext);

        LoginResult result = await useCase.ExecuteAsync(new LoginRequest("admin", "1234"), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var logs = await GetLogsAsync(auditRepository, userRepository, admin.Id, TestContext.Current.CancellationToken);
        AuditLogEntry loginLog = Assert.Single(logs, l => l.Action == "Login");
        Assert.Equal("admin", loginLog.Username);
        Assert.Equal(admin.Id, loginLog.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPasswordIsWrong_WritesLoginFailedAuditEntry()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var userRepository = new UserRepository(dbContext);
        var admin = new User("admin", "hash:1234", UserRole.Administrator);
        await userRepository.AddAsync(admin, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var auditRepository = new AuditLogRepository(dbContext);
        var useCase = new LoginUseCase(userRepository, new FakePasswordHashService(), auditRepository, dbContext);

        LoginResult result = await useCase.ExecuteAsync(new LoginRequest("admin", "wrong"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        var logs = await GetLogsAsync(auditRepository, userRepository, admin.Id, TestContext.Current.CancellationToken);
        AuditLogEntry failedLog = Assert.Single(logs, l => l.Action == "LoginFailed");
        Assert.Equal("admin", failedLog.Username);
        Assert.Equal(admin.Id, failedLog.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUsernameDoesNotExist_WritesLoginFailedAuditEntryWithoutUserId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var userRepository = new UserRepository(dbContext);
        var admin = new User("admin", "hash:1234", UserRole.Administrator);
        await userRepository.AddAsync(admin, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var auditRepository = new AuditLogRepository(dbContext);
        var useCase = new LoginUseCase(userRepository, new FakePasswordHashService(), auditRepository, dbContext);

        LoginResult result = await useCase.ExecuteAsync(new LoginRequest("ghost", "1234"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        var logs = await GetLogsAsync(auditRepository, userRepository, admin.Id, TestContext.Current.CancellationToken);
        AuditLogEntry failedLog = Assert.Single(logs, l => l.Action == "LoginFailed");
        Assert.Equal("ghost", failedLog.Username);
        Assert.Null(failedLog.UserId);
    }

    [Fact]
    public async Task LogoutUseCase_WritesLogoutAuditEntry()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var userRepository = new UserRepository(dbContext);
        var admin = new User("admin", "hash:1234", UserRole.Administrator);
        await userRepository.AddAsync(admin, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var auditRepository = new AuditLogRepository(dbContext);
        var useCase = new LogoutUseCase(auditRepository, dbContext);

        await useCase.ExecuteAsync(admin.Id, "admin", TestContext.Current.CancellationToken);

        var logs = await GetLogsAsync(auditRepository, userRepository, admin.Id, TestContext.Current.CancellationToken);
        AuditLogEntry logoutLog = Assert.Single(logs, l => l.Action == "Logout");
        Assert.Equal("admin", logoutLog.Username);
        Assert.Equal(admin.Id, logoutLog.UserId);
    }

    private static Task<IReadOnlyList<AuditLogEntry>> GetLogsAsync(
        AuditLogRepository auditRepository,
        UserRepository userRepository,
        Guid requestingUserId,
        CancellationToken ct)
        => new GetAuditLogsUseCase(auditRepository, userRepository).ExecuteAsync(requestingUserId, 10, null, ct);

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FakePasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password) => $"hash:{password}";

        public bool VerifyPassword(string password, string passwordHash) => passwordHash == $"hash:{password}";
    }
}
