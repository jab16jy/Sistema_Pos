using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs.Audit;
using POS.Application.Interfaces;
using POS.Application.UseCases.Audit;
using POS.Application.UseCases.Auth;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
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
        var admin = await SeedAdminAsync(dbContext, repository, TestContext.Current.CancellationToken);
        var useCase = new CreateUserUseCase(repository, new FakePasswordHashService(), dbContext);

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
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var useCase = new CreateUserUseCase(repository, new FakePasswordHashService(), dbContext);

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
        var admin = await SeedAdminAsync(dbContext, repository, TestContext.Current.CancellationToken);
        var useCase = new CreateUserUseCase(repository, new FakePasswordHashService(), dbContext);

        await useCase.ExecuteAsync("cajero1", "1234", UserRole.Cashier, admin.Id, TestContext.Current.CancellationToken);

        // GetRecentAsync used to ORDER BY CreatedAt (DateTimeOffset) at the SQL level, which threw
        // NotSupportedException on SQLite; it now orders client-side and reads the entries the
        // SaveChanges override auto-wrote.
        System.Collections.Generic.IReadOnlyList<AuditLogEntry> logs =
            await new GetAuditLogsUseCase(new AuditLogRepository(dbContext)).ExecuteAsync(10, null, TestContext.Current.CancellationToken);

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
                    .ExecuteAsync(10, null, TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task SaveChanges_AutoAudits_SaleUsesInvoiceNumberInsteadOfRawGuid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection, new FakeSession("admin"));
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var sale = new Sale(Guid.NewGuid());
        await dbContext.Sales.AddAsync(sale, TestContext.Current.CancellationToken);

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLogEntry> logs =
            await new GetAuditLogsUseCase(new AuditLogRepository(dbContext)).ExecuteAsync(10, null, TestContext.Current.CancellationToken);

        // An admin reading the audit log can't interpret a raw Guid; the invoice number is what they see on receipts/screen.
        AuditLogEntry saleLog = Assert.Single(logs, l => l.EntityName == "Sale");
        Assert.Equal(sale.InvoiceNumber, saleLog.EntityId);
    }

    [Fact]
    public async Task SaveChanges_AutoAudits_CustomerUsesIdentificationInsteadOfRawGuid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection, new FakeSession("admin"));
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var customer = new Customer("Juan Perez", "123456789");
        await dbContext.Customers.AddAsync(customer, TestContext.Current.CancellationToken);

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLogEntry> logs =
            await new GetAuditLogsUseCase(new AuditLogRepository(dbContext)).ExecuteAsync(10, null, TestContext.Current.CancellationToken);

        // "el id del cliente" the admin recognizes is the cédula/NIT (Identification), not the internal Guid.
        AuditLogEntry customerLog = Assert.Single(logs, l => l.EntityName == "Customer");
        Assert.Equal(customer.Identification, customerLog.EntityId);
    }

    [Fact]
    public async Task SaveChanges_AutoAudits_DeletedCustomerCapturesNameAtWriteTime()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection, new FakeSession("admin"));
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var customer = new Customer("Cristian Ramirez", "123456789");
        await dbContext.Customers.AddAsync(customer, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        dbContext.Customers.Remove(customer);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLogEntry> logs =
            await new GetAuditLogsUseCase(new AuditLogRepository(dbContext)).ExecuteAsync(10, null, TestContext.Current.CancellationToken);

        // The row is gone after delete, so the name must be baked into the detail when the delete is audited.
        AuditLogEntry deleteLog = Assert.Single(logs, l => l.EntityName == "Customer" && l.Action == "Deleted");
        Assert.Contains("Cristian Ramirez", deleteLog.Details);
    }

    [Fact]
    public async Task SaveChanges_AutoAudits_InventoryMovementDescribesWhatChanged()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection, new FakeSession("admin"));
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var product = new Product("Azucar", Guid.NewGuid(), new Money(1_000m), new Money(1_500m), new StockQuantity(10m), new StockQuantity(2m));
        await dbContext.Products.AddAsync(product, TestContext.Current.CancellationToken);
        var movement = new InventoryMovement(product.Id, InventoryMovementType.Entry, new Quantity(5m), "Compra a proveedor", Guid.NewGuid());
        await dbContext.InventoryMovements.AddAsync(movement, TestContext.Current.CancellationToken);

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLogEntry> logs =
            await new GetAuditLogsUseCase(new AuditLogRepository(dbContext)).ExecuteAsync(10, null, TestContext.Current.CancellationToken);

        // A raw "Registro creado." tells the admin nothing; they need the product, quantity and reason.
        AuditLogEntry movementLog = Assert.Single(logs, l => l.EntityName == "InventoryMovement");
        Assert.Contains("Azucar", movementLog.Details);
        Assert.Contains("Entrada", movementLog.Details);
        Assert.Contains("Compra a proveedor", movementLog.Details);
    }

    private static async Task<User> SeedAdminAsync(AppDbContext dbContext, UserRepository repository, CancellationToken ct)
    {
        var admin = new User("admin-seed", "hash", UserRole.Administrator);
        await repository.AddAsync(admin, ct);
        await dbContext.SaveChangesAsync(ct);
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
