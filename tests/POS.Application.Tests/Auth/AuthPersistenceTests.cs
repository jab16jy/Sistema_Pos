using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs.Audit;
using POS.Application.Interfaces;
using POS.Application.UseCases.Audit;
using POS.Application.UseCases.Auth;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests.Auth;

public sealed class AuthPersistenceTests
{
    [Fact]
    public async Task CreateUserUseCase_PersistsUserSoItSurvivesQuery()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var repository = new UserRepository(dbContext);
        var admin = await SeedAdminAsync(repository, TestContext.Current.CancellationToken);
        var useCase = new CreateUserUseCase(repository, new FakePasswordHashService());

        (bool ok, string? error) = await useCase.ExecuteAsync("cajero1", "1234", UserRole.Cashier, admin.Id, TestContext.Current.CancellationToken);

        Assert.True(ok, error);
        // Regression: before the fix AddAsync only staged the entity and never saved, so this query returned nothing.
        Assert.Equal(2, await dbContext.Users.AsNoTracking().CountAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(await repository.GetByUsernameAsync("cajero1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateUserUseCase_WhenRequestingUserIsNotAdministrator_IsRejected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var repository = new UserRepository(dbContext);
        var cashier = new User("cajero-actual", "hash", UserRole.Cashier);
        await repository.AddAsync(cashier, TestContext.Current.CancellationToken);
        var useCase = new CreateUserUseCase(repository, new FakePasswordHashService());

        (bool ok, string? error) = await useCase.ExecuteAsync("nuevo", "1234", UserRole.Administrator, cashier.Id, TestContext.Current.CancellationToken);

        Assert.False(ok);
        Assert.Contains("administrador", error, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await repository.GetByUsernameAsync("nuevo", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveChanges_AutoAudits_AndAuditIsReadableOnSqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection, new FakeSession("admin"));
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var repository = new UserRepository(dbContext);
        var admin = await SeedAdminAsync(repository, TestContext.Current.CancellationToken);
        var useCase = new CreateUserUseCase(repository, new FakePasswordHashService());

        await useCase.ExecuteAsync("cajero1", "1234", UserRole.Cashier, admin.Id, TestContext.Current.CancellationToken);

        // GetRecentAsync used to ORDER BY CreatedAt (DateTimeOffset) at the SQL level, which threw
        // NotSupportedException on SQLite; it now orders client-side and reads the entries the
        // SaveChanges override auto-wrote.
        System.Collections.Generic.IReadOnlyList<AuditLogEntry> logs =
            await new GetAuditLogsUseCase(new AuditLogRepository(dbContext)).ExecuteAsync(10, TestContext.Current.CancellationToken);

        Assert.Contains(logs, l => l.EntityName == "User" && l.Username == "admin");
    }

    [Fact]
    public async Task AutoAudit_ThroughRealDependencyInjection_CapturesTheLoggedInOperator()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"pos-audit-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection();
            services.AddPosInfrastructure($"Data Source={dbPath}");
            await using ServiceProvider provider = services.BuildServiceProvider();
            await DatabaseSchemaRollout.EnsureLatestSchemaAsync(provider, TestContext.Current.CancellationToken);

            Guid adminId = Guid.NewGuid();
            using (IServiceScope seedScope = provider.CreateScope())
            {
                var users = seedScope.ServiceProvider.GetRequiredService<POS.Domain.Interfaces.IUserRepository>();
                var admin = new User("carlos", "hash", UserRole.Administrator);
                adminId = admin.Id;
                await users.AddAsync(admin, TestContext.Current.CancellationToken);
            }

            // Simulate a logged-in operator, then perform an audited write in a request scope.
            provider.GetRequiredService<IWritableOperatorSession>().SetUser(adminId, "carlos", UserRole.Administrator);

            using (IServiceScope scope = provider.CreateScope())
            {
                var createUser = scope.ServiceProvider.GetRequiredService<CreateUserUseCase>();
                (bool ok, string? error) = await createUser.ExecuteAsync("cajero1", "1234", UserRole.Cashier, adminId, TestContext.Current.CancellationToken);
                Assert.True(ok, error);
            }

            using (IServiceScope scope = provider.CreateScope())
            {
                IReadOnlyList<AuditLogEntry> logs = await scope.ServiceProvider
                    .GetRequiredService<GetAuditLogsUseCase>()
                    .ExecuteAsync(10, TestContext.Current.CancellationToken);

                // The crux of "quién cambió qué": EF injects the session so the actor is the operator, not "Sistema".
                Assert.Contains(logs, l => l.EntityName == "User" && l.Username == "carlos");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    private static async Task<User> SeedAdminAsync(UserRepository repository, CancellationToken ct)
    {
        var admin = new User("admin-seed", "hash", UserRole.Administrator);
        await repository.AddAsync(admin, ct);
        return admin;
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection, ICurrentOperatorSession? session = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options, session);
    }

    private sealed class FakePasswordHashService : IPasswordHashService
    {
        public string HashPassword(string password) => $"hash:{password}";

        public bool VerifyPassword(string password, string passwordHash) => passwordHash == $"hash:{password}";
    }

    private sealed class FakeSession : ICurrentOperatorSession
    {
        public FakeSession(string username) => Username = username;

        public Guid OperatorId => Guid.NewGuid();
        public string Username { get; }
        public UserRole Role => UserRole.Administrator;
        public bool IsAuthenticated => true;
    }
}
