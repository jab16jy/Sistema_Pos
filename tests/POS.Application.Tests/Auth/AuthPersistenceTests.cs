using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.DTOs.Audit;
using POS.Application.Interfaces;
using POS.Application.UseCases.Audit;
using POS.Application.UseCases.Auth;
using POS.Application.Tests;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Domain.Interfaces;
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
        var useCase = new CreateUserUseCase(repository, new RoleRepository(dbContext), new FakePasswordHashService(), dbContext);

        (bool ok, string? error) = await useCase.ExecuteAsync("cajero1", "1234", POS.Domain.Authorization.RoleConstants.CashierRoleId, admin.Id, TestContext.Current.CancellationToken);

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
        var cashier = new User("cajero-actual", "hash", POS.Domain.Authorization.RoleConstants.CashierRoleId);
        await repository.AddAsync(cashier, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var useCase = new CreateUserUseCase(repository, new RoleRepository(dbContext), new FakePasswordHashService(), dbContext);

        (bool ok, string? error) = await useCase.ExecuteAsync("nuevo", "1234", POS.Domain.Authorization.RoleConstants.AdministratorRoleId, cashier.Id, TestContext.Current.CancellationToken);

        Assert.False(ok);
        Assert.Contains("permiso", error, StringComparison.OrdinalIgnoreCase);
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
        var useCase = new CreateUserUseCase(repository, new RoleRepository(dbContext), new FakePasswordHashService(), dbContext);

        await useCase.ExecuteAsync("cajero1", "1234", POS.Domain.Authorization.RoleConstants.CashierRoleId, admin.Id, TestContext.Current.CancellationToken);

        System.Collections.Generic.IReadOnlyList<AuditLogEntry> logs =
            await new GetAuditLogsUseCase(new AuditLogRepository(dbContext), repository, new RoleRepository(dbContext))
                .ExecuteAsync(admin.Id, 10, null, TestContext.Current.CancellationToken);

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
                var admin = new User("carlos", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
                adminId = admin.Id;
                await users.AddAsync(admin, TestContext.Current.CancellationToken);
                var unitOfWork = seedScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                await unitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Simulate a logged-in operator, then perform an audited write in a request scope.
            provider.GetRequiredService<IWritableOperatorSession>().SetUser(adminId, "carlos", POS.Domain.Authorization.RoleConstants.AdministratorRoleId, POS.Domain.Authorization.RoleConstants.AdministratorRoleName, new HashSet<string>(POS.Domain.Authorization.PermissionKeys.All));

            using (IServiceScope scope = provider.CreateScope())
            {
                var createUser = scope.ServiceProvider.GetRequiredService<CreateUserUseCase>();
                (bool ok, string? error) = await createUser.ExecuteAsync("cajero1", "1234", POS.Domain.Authorization.RoleConstants.CashierRoleId, adminId, TestContext.Current.CancellationToken);
                Assert.True(ok, error);
            }

            using (IServiceScope scope = provider.CreateScope())
            {
                IReadOnlyList<AuditLogEntry> logs = await scope.ServiceProvider
                    .GetRequiredService<GetAuditLogsUseCase>()
                    .ExecuteAsync(adminId, 10, null, TestContext.Current.CancellationToken);

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
        var userRepository = new UserRepository(dbContext);
        var admin = await SeedAdminAsync(dbContext, userRepository, TestContext.Current.CancellationToken);
        var sale = new Sale(admin.Id);
        await dbContext.Sales.AddAsync(sale, TestContext.Current.CancellationToken);

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLogEntry> logs =
            await new GetAuditLogsUseCase(new AuditLogRepository(dbContext), userRepository, new RoleRepository(dbContext))
                .ExecuteAsync(admin.Id, 10, null, TestContext.Current.CancellationToken);

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
        var userRepository = new UserRepository(dbContext);
        var admin = await SeedAdminAsync(dbContext, userRepository, TestContext.Current.CancellationToken);
        var customer = new Customer("Juan Perez", "123456789");
        await dbContext.Customers.AddAsync(customer, TestContext.Current.CancellationToken);

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLogEntry> logs =
            await new GetAuditLogsUseCase(new AuditLogRepository(dbContext), userRepository, new RoleRepository(dbContext))
                .ExecuteAsync(admin.Id, 10, null, TestContext.Current.CancellationToken);

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
        var userRepository = new UserRepository(dbContext);
        var admin = await SeedAdminAsync(dbContext, userRepository, TestContext.Current.CancellationToken);
        var customer = new Customer("Cristian Ramirez", "123456789");
        await dbContext.Customers.AddAsync(customer, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        dbContext.Customers.Remove(customer);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLogEntry> logs =
            await new GetAuditLogsUseCase(new AuditLogRepository(dbContext), userRepository, new RoleRepository(dbContext))
                .ExecuteAsync(admin.Id, 10, null, TestContext.Current.CancellationToken);

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
        var userRepository = new UserRepository(dbContext);
        var admin = await SeedAdminAsync(dbContext, userRepository, TestContext.Current.CancellationToken);
        Product product = await TestProductFactory.CreateAndPersistAsync(dbContext, "Azucar", new Money(1_000m), new Money(1_500m), new StockQuantity(10m), new StockQuantity(2m), TestContext.Current.CancellationToken);
        var movement = new InventoryMovement(product.Id, InventoryMovementType.Entry, new Quantity(5m), "Compra a proveedor", Guid.NewGuid());
        await dbContext.InventoryMovements.AddAsync(movement, TestContext.Current.CancellationToken);

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLogEntry> logs =
            await new GetAuditLogsUseCase(new AuditLogRepository(dbContext), userRepository, new RoleRepository(dbContext))
                .ExecuteAsync(admin.Id, 10, null, TestContext.Current.CancellationToken);

        // A raw "Registro creado." tells the admin nothing; they need the product, quantity and reason.
        AuditLogEntry movementLog = Assert.Single(logs, l => l.EntityName == "InventoryMovement");
        Assert.Contains("Azucar", movementLog.Details);
        Assert.Contains("Entrada", movementLog.Details);
        Assert.Contains("Compra a proveedor", movementLog.Details);
    }

    [Fact]
    public async Task GetAuditLogsUseCase_WhenRequestingUserIsNotAdministrator_Throws()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var userRepository = new UserRepository(dbContext);
        var cashier = new User("cajero", "hash", POS.Domain.Authorization.RoleConstants.CashierRoleId);
        await userRepository.AddAsync(cashier, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var useCase = new GetAuditLogsUseCase(new AuditLogRepository(dbContext), userRepository, new RoleRepository(dbContext));

        await Assert.ThrowsAsync<POS.Domain.Exceptions.UnauthorizedActionException>(
            () => useCase.ExecuteAsync(cashier.Id, 10, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AuditLogRepository_GetRecentAsync_PaginatesAtSqlLevelInsteadOfLoadingWholeTable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection, new FakeSession("admin"));
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var userRepository = new UserRepository(dbContext);
        var admin = await SeedAdminAsync(dbContext, userRepository, TestContext.Current.CancellationToken);

        // Each Customer add triggers one auto-audited "Added" entry via the interceptor.
        for (int i = 0; i < 5; i++)
        {
            await dbContext.Customers.AddAsync(new Customer($"Cliente {i}", $"ID-{i}"), TestContext.Current.CancellationToken);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var repository = new AuditLogRepository(dbContext);
        var page1 = await repository.GetRecentAsync(2, new AuditLogFilter(Page: 1), TestContext.Current.CancellationToken);
        var page2 = await repository.GetRecentAsync(2, new AuditLogFilter(Page: 2), TestContext.Current.CancellationToken);

        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
        // Pages must not overlap and must stay newest-first across the page boundary.
        Assert.DoesNotContain(page1[0].Id, page2.Select(l => l.Id));
        Assert.True(page1[^1].CreatedAt >= page2[0].CreatedAt);
    }

    [Fact]
    public async Task AuditLogRepository_GetRecentAsync_OrdersCorrectlyAgainstPreExistingLegacyFormatRows()
    {
        // Regression guard: AuditLog.CreatedAt now uses an explicit string HasConversion
        // ("yyyy-MM-dd HH:mm:ss.fffffffzzz") instead of an unconverted DateTimeOffset mapping, so
        // that ORDER BY translates to SQL. Real installs already have rows written by EF/SQLite's
        // DEFAULT DateTimeOffset-to-TEXT serialization, which trims trailing fractional zero
        // digits (e.g. "2026-07-09 10:00:00.5+00:00" instead of a fixed 7-digit ".5000000"). This
        // test writes a row in that legacy trimmed format directly via raw SQL (bypassing EF's
        // conversion, simulating a pre-existing production row) and confirms it still sorts
        // correctly against a new row written through the fixed-width conversion.
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection, new FakeSession("admin"));
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var userRepository = new UserRepository(dbContext);
        var admin = await SeedAdminAsync(dbContext, userRepository, TestContext.Current.CancellationToken);

        // Legacy row: an instant far in the past, written in the trimmed default format (no
        // HasConversion) -- guaranteed earlier than DateTimeOffset.UtcNow used by the new row below.
        var legacyId = Guid.NewGuid();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText =
                "INSERT INTO AUDIT_LOGS (Id, UserId, Username, Action, EntityName, EntityId, Details, CreatedAt) " +
                "VALUES ($id, $userId, 'legacy-admin', 'Added', 'Customer', 'LEGACY-1', NULL, '2020-01-01 10:00:00.5+00:00')";
            cmd.Parameters.AddWithValue("$id", legacyId.ToString());
            cmd.Parameters.AddWithValue("$userId", admin.Id.ToString());
            await cmd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        // New row: later instant, written through the fixed-width EF conversion (real code path).
        await dbContext.Customers.AddAsync(new Customer("Cliente Nuevo", "ID-NEW"), TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new AuditLogRepository(dbContext);
        var logs = await repository.GetRecentAsync(10, null, TestContext.Current.CancellationToken);

        // Newest-first: the new row (auto-audited Customer add) must come before the legacy row.
        int legacyIndex = logs.ToList().FindIndex(l => l.Id == legacyId);
        int newIndex = logs.ToList().FindIndex(l => l.EntityName == "Customer" && l.EntityId == "ID-NEW");
        Assert.True(legacyIndex >= 0, "Legacy-format row must still be readable.");
        Assert.True(newIndex >= 0, "New row must be present.");
        Assert.True(newIndex < legacyIndex, "Newer row must sort before the older legacy-format row.");
    }

    [Fact]
    public async Task SaveChanges_AutoAudits_UserAndSupplierUseBusinessReferenceInsteadOfRawGuid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection, new FakeSession("admin"));
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var userRepository = new UserRepository(dbContext);
        var admin = await SeedAdminAsync(dbContext, userRepository, TestContext.Current.CancellationToken);
        var supplier = new Supplier("Distribuidora ACME", "900123456-1");
        await dbContext.Suppliers.AddAsync(supplier, TestContext.Current.CancellationToken);

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLogEntry> logs =
            await new GetAuditLogsUseCase(new AuditLogRepository(dbContext), userRepository, new RoleRepository(dbContext))
                .ExecuteAsync(admin.Id, 10, null, TestContext.Current.CancellationToken);

        // Both are business identifiers an admin recognizes; the raw Guid would not be.
        AuditLogEntry userLog = Assert.Single(logs, l => l.EntityName == "User" && l.Username == "admin" && l.Action == "Added");
        Assert.Equal("admin-seed", userLog.EntityId);
        AuditLogEntry supplierLog = Assert.Single(logs, l => l.EntityName == "Supplier");
        Assert.Equal("900123456-1", supplierLog.EntityId);
    }

    [Fact]
    public async Task UserRepository_AddAndUpdate_AreStageOnlyUntilUnitOfWorkSaves()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var repository = new UserRepository(dbContext);
        var user = new User("stage-user", "hash", POS.Domain.Authorization.RoleConstants.CashierRoleId);

        await repository.AddAsync(user, TestContext.Current.CancellationToken);

        await using (var checkContext = CreateDbContext(connection))
        {
            Assert.Equal(0, await checkContext.Users.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, await checkContext.Roles.CountAsync(TestContext.Current.CancellationToken));
        }

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using (var checkContext = CreateDbContext(connection))
        {
            Assert.Equal(1, await checkContext.Users.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(2, await checkContext.Roles.CountAsync(TestContext.Current.CancellationToken));
        }

        user.Deactivate();
        await repository.UpdateAsync(user, TestContext.Current.CancellationToken);

        await using (var checkContext = CreateDbContext(connection))
        {
            User uncommittedUser = await checkContext.Users.SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
            Assert.True(uncommittedUser.IsActive);
        }

        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using (var checkContext = CreateDbContext(connection))
        {
            User committedUser = await checkContext.Users.SingleAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken);
            Assert.False(committedUser.IsActive);
        }
    }

    [Fact]
    public async Task CreateUserUseCase_WhenRoleBootstrapFails_PersistsNeitherUserNorRolePartialState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var repository = new UserRepository(dbContext);
        var user = new User("newadmin", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);

        await repository.AddAsync(user, TestContext.Current.CancellationToken);

        var failingUnitOfWork = new FailingUnitOfWork();
        await Assert.ThrowsAsync<InvalidOperationException>(() => failingUnitOfWork.SaveChangesAsync(TestContext.Current.CancellationToken));

        await using var checkContext = CreateDbContext(connection);
        Assert.Equal(0, await checkContext.Roles.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await checkContext.Users.CountAsync(TestContext.Current.CancellationToken));
    }

    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromException<int>(new InvalidOperationException("Simulated save failure."));

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private static async Task<User> SeedAdminAsync(AppDbContext dbContext, UserRepository repository, CancellationToken ct)
    {
        var admin = new User("admin-seed", "hash", POS.Domain.Authorization.RoleConstants.AdministratorRoleId);
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
        public Guid RoleId => POS.Domain.Authorization.RoleConstants.AdministratorRoleId;
        public string RoleName => POS.Domain.Authorization.RoleConstants.AdministratorRoleName;
        public IReadOnlySet<string> Permissions => new HashSet<string>(POS.Domain.Authorization.PermissionKeys.All);
        public bool HasPermission(string permissionKey) => Permissions.Contains(permissionKey);
        public bool IsAuthenticated => true;
    }
}
