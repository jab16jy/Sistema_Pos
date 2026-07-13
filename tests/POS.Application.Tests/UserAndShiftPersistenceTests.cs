using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
using POS.Domain.Entities;
using POS.Domain.Authorization;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using POS.Infrastructure;
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
        await SeedSystemRolesAsync(dbContext);
        var admin = new User("admin", "hash:admin", RoleConstants.AdministratorRoleId);
        await dbContext.Users.AddAsync(admin, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new CreateUserUseCase(new UserRepository(dbContext), new RoleRepository(dbContext), new FakePasswordHashService(), dbContext);

        var result = await useCase.ExecuteAsync(
            "admin2",
            "1234",
            RoleConstants.AdministratorRoleId,
            admin.Id,
            TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.Error);
        Assert.Equal(2, await dbContext.Users.CountAsync(TestContext.Current.CancellationToken));
        var persistedUser = await dbContext.Users.SingleAsync(user => user.Username == "admin2", TestContext.Current.CancellationToken);
        Assert.Equal("admin2", persistedUser.Username);
        Assert.Equal(RoleConstants.AdministratorRoleId, persistedUser.RoleId);
    }

    [Fact]
    public async Task CreateUserUseCase_WhenRequesterIsCashier_DoesNotCreateUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedSystemRolesAsync(dbContext);
        var cashier = new User("cashier", "hash:cashier", RoleConstants.CashierRoleId);
        await dbContext.Users.AddAsync(cashier, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var useCase = new CreateUserUseCase(new UserRepository(dbContext), new RoleRepository(dbContext), new FakePasswordHashService(), dbContext);

        var result = await useCase.ExecuteAsync(
            "admin2",
            "1234",
            RoleConstants.AdministratorRoleId,
            cashier.Id,
            TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("permiso", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await dbContext.Users.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAndDeactivateUserUseCases_WhenRequesterIsCashier_RejectAdminOperations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await SeedSystemRolesAsync(dbContext);
        var cashier = new User("cashier", "hash:cashier", RoleConstants.CashierRoleId);
        var target = new User("target", "hash:target", RoleConstants.CashierRoleId);
        await dbContext.Users.AddRangeAsync([cashier, target], TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new UserRepository(dbContext);

        var updateResult = await new UpdateUserUseCase(repository, new RoleRepository(dbContext), new FakePasswordHashService())
            .ExecuteAsync(target.Id, null, RoleConstants.AdministratorRoleId, cashier.Id, TestContext.Current.CancellationToken);
        var deactivateResult = await new DeactivateUserUseCase(repository, new RoleRepository(dbContext))
            .ExecuteAsync(target.Id, cashier.Id, TestContext.Current.CancellationToken);

        Assert.False(updateResult.Success);
        Assert.False(deactivateResult.Success);
        Assert.Contains("permiso", updateResult.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("permiso", deactivateResult.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RoleConstants.CashierRoleId, target.RoleId);
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

    [Fact]
    public async Task RoleRepositoryAndSchemaSeeding_PersistExactSystemRoleStateAndGuidLookups()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"pos-rbac-{Guid.NewGuid():N}.db");
        await using var provider = new ServiceCollection().AddPosInfrastructure($"Data Source={databasePath}").BuildServiceProvider();
        try
        {
            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);
            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            using IServiceScope scope = provider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

            Assert.Equal(2, await dbContext.Roles.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(RoleConstants.AdministratorRoleId, (await dbContext.Users.SingleAsync(TestContext.Current.CancellationToken)).RoleId);
            Assert.Empty(await dbContext.RolePermissions.Where(permission => permission.RoleId == RoleConstants.AdministratorRoleId).ToListAsync(TestContext.Current.CancellationToken));
            Assert.True(PermissionKeys.CashierDefaults.SetEquals((await roles.GetPermissionKeysAsync(RoleConstants.CashierRoleId, TestContext.Current.CancellationToken))));

            var customRole = new Role("Report Viewer");
            await roles.AddAsync(customRole, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            await roles.ReplacePermissionsAsync(customRole.Id, new HashSet<string> { PermissionKeys.Reports.View }, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.True(await roles.HasPermissionAsync(customRole.Id, PermissionKeys.Reports.View, TestContext.Current.CancellationToken));

            var user = new User("viewer", "hash:password", customRole.Id);
            await dbContext.Users.AddAsync(user, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.Equal(user.Id, (await dbContext.Users.SingleAsync(candidate => candidate.Id == user.Id, TestContext.Current.CancellationToken)).Id);

            var login = new LoginUseCase(
                new UserRepository(dbContext),
                roles,
                new FakePasswordHashService(),
                new AuditLogRepository(dbContext),
                dbContext);
            var firstLogin = await login.ExecuteAsync(new POS.Application.DTOs.Auth.LoginRequest("viewer", "password"), TestContext.Current.CancellationToken);
            var session = new InMemoryUserSession();
            session.SetUser(firstLogin.UserId, firstLogin.Username, firstLogin.RoleId, firstLogin.RoleName, firstLogin.Permissions!);
            Assert.Equal(customRole.Id, firstLogin.RoleId);
            Assert.True(session.HasPermission(PermissionKeys.Reports.View));

            await roles.ReplacePermissionsAsync(customRole.Id, new HashSet<string> { PermissionKeys.Inventory.View }, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.True(session.HasPermission(PermissionKeys.Reports.View));
            Assert.False(session.HasPermission(PermissionKeys.Inventory.View));
            var secondLogin = await login.ExecuteAsync(new POS.Application.DTOs.Auth.LoginRequest("viewer", "password"), TestContext.Current.CancellationToken);
            Assert.True(secondLogin.Permissions!.SetEquals(new HashSet<string> { PermissionKeys.Inventory.View }));

            var administrator = new User("admin-snapshot", "hash:password", RoleConstants.AdministratorRoleId);
            await dbContext.Users.AddAsync(administrator, TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            var adminLogin = await login.ExecuteAsync(new POS.Application.DTOs.Auth.LoginRequest("admin-snapshot", "password"), TestContext.Current.CancellationToken);
            Assert.True(PermissionKeys.All.All(adminLogin.Permissions!.Contains));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

    private static async Task SeedSystemRolesAsync(AppDbContext dbContext)
    {
        dbContext.Roles.AddRange(
            Role.CreateSystemRole(RoleConstants.AdministratorRoleId, RoleConstants.AdministratorRoleName),
            Role.CreateSystemRole(RoleConstants.CashierRoleId, RoleConstants.CashierRoleName));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed class FakePasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password) => $"hash:{password}";

        public bool VerifyPassword(string password, string passwordHash) => passwordHash == $"hash:{password}";
    }
}
