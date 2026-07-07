using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests;

public sealed class UserAndShiftPersistenceTests
{
    [Fact]
    public async Task CreateUserUseCase_WithSqliteRepository_PersistsCreatedUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var admin = new User("admin", "hash:admin", UserRole.Administrator);
        await dbContext.Users.AddAsync(admin, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new CreateUserUseCase(new UserRepository(dbContext), new FakePasswordHashService(), dbContext);

        var result = await useCase.ExecuteAsync(
            "admin2",
            "1234",
            UserRole.Administrator,
            admin.Id,
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, await dbContext.Users.CountAsync(TestContext.Current.CancellationToken));
        var persistedUser = await dbContext.Users.SingleAsync(user => user.Username == "admin2", TestContext.Current.CancellationToken);
        Assert.Equal("admin2", persistedUser.Username);
        Assert.Equal(UserRole.Administrator, persistedUser.Role);
    }

    [Fact]
    public async Task CreateUserUseCase_WhenRequesterIsCashier_DoesNotCreateUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var cashier = new User("cashier", "hash:cashier", UserRole.Cashier);
        await dbContext.Users.AddAsync(cashier, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new CreateUserUseCase(new UserRepository(dbContext), new FakePasswordHashService(), dbContext);

        var result = await useCase.ExecuteAsync(
            "admin2",
            "1234",
            UserRole.Administrator,
            cashier.Id,
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("administrador", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await dbContext.Users.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAndDeactivateUserUseCases_WhenRequesterIsCashier_RejectAdminOperations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var cashier = new User("cashier", "hash:cashier", UserRole.Cashier);
        var target = new User("target", "hash:target", UserRole.Cashier);
        await dbContext.Users.AddRangeAsync([cashier, target], TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new UserRepository(dbContext);

        var updateResult = await new UpdateUserUseCase(repository, new FakePasswordHashService())
            .ExecuteAsync(target.Id, null, UserRole.Administrator, cashier.Id, TestContext.Current.CancellationToken);
        var deactivateResult = await new DeactivateUserUseCase(repository)
            .ExecuteAsync(target.Id, cashier.Id, TestContext.Current.CancellationToken);

        Assert.False(updateResult.Success);
        Assert.False(deactivateResult.Success);
        Assert.Contains("administrador", updateResult.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("administrador", deactivateResult.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(UserRole.Cashier, target.Role);
        Assert.True(target.IsActive);
    }

    [Fact]
    public async Task CashierShiftRepository_GetRecentAsync_WithSqlite_DoesNotOrderDateTimeOffsetInDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var shift = new CashierShift(Guid.NewGuid(), "cashier", new Money(10_000m));
        await dbContext.CashierShifts.AddAsync(shift, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new CashierShiftRepository(dbContext);

        var recent = await repository.GetRecentAsync(10, TestContext.Current.CancellationToken);

        CashierShift persistedShift = Assert.Single(recent);
        Assert.Equal(shift.Id, persistedShift.Id);
    }

    [Fact]
    public async Task AuditLogRepository_GetRecentAsync_WithSqlite_DoesNotOrderDateTimeOffsetInDatabase()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var auditLog = new AuditLog(Guid.NewGuid(), "admin", "Added", "User", Guid.NewGuid().ToString(), "Registro creado.");
        await dbContext.AuditLogs.AddAsync(auditLog, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new AuditLogRepository(dbContext);

        var recent = await repository.GetRecentAsync(10, cancellationToken: TestContext.Current.CancellationToken);

        AuditLog persistedLog = Assert.Single(recent);
        Assert.Equal(auditLog.Id, persistedLog.Id);
    }

    [Fact]
    public async Task AuditLogRepository_GetRecentAsync_AppliesOptionFiltersBeforeTakingResults()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await dbContext.AuditLogs.AddRangeAsync([
            new AuditLog(Guid.NewGuid(), "admin", "Added", "User", Guid.NewGuid().ToString(), "Registro creado."),
            new AuditLog(Guid.NewGuid(), "cashier", "Modified", "Sale", Guid.NewGuid().ToString(), "Registro actualizado.")
        ], TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new AuditLogRepository(dbContext);

        var filtered = await repository.GetRecentAsync(
            10,
            new AuditLogFilter(Username: "cash", Action: "Modified", EntityName: "Sale"),
            TestContext.Current.CancellationToken);

        AuditLog persistedLog = Assert.Single(filtered);
        Assert.Equal("cashier", persistedLog.Username);
        Assert.Equal("Modified", persistedLog.Action);
        Assert.Equal("Sale", persistedLog.EntityName);
    }

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
