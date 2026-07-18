using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Invoices;
using POS.Application.Interfaces;
using POS.Application.Services;
using POS.Application.UseCases.Invoices;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests.Audit;

public sealed class CanonicalActorAuditTests
{
    [Fact]
    public async Task ExecuteAsync_UsesCanonicalActorForReturnInventoryAndAudit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var role = Role.CreateSystemRole(Guid.NewGuid(), "Cashier");
        User actor = new("return-actor", "hash", role.Id);
        var session = new Session(actor.Id, actor.Username);
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options, session);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        db.AddRange(role, actor);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var category = new Category("Returns");
        var product = new Product("Product", category.Id, new Money(5m), new Money(10m), new StockQuantity(10m), new StockQuantity(0m));
        var sale = new Sale(actor.Id);
        sale.AddItem(product, new Quantity(1m), Money.Zero);
        sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(10m), new Money(10m)));
        sale.Complete();
        db.AddRange(category, product, sale);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.AuditLogs.RemoveRange(db.AuditLogs);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var users = new Users(actor);
        var useCase = new ProcessReturnUseCase(
            new SaleRepository(db), new ProductRepository(db), new InventoryRepository(db), new SaleReturnRepository(db), db, users,
            receivables: new ReceivableRepository(db),
            authorizer: new CriticalCommandAuthorizer(session, users, new Roles()));

        await useCase.ExecuteAsync(new ProcessReturnRequest(sale.Id, "Customer return", Guid.NewGuid(), ReturnResolutionChoice.Cash), TestContext.Current.CancellationToken);

        SaleReturn saleReturn = await db.SaleReturns.SingleAsync(TestContext.Current.CancellationToken);
        InventoryMovement movement = await db.InventoryMovements.SingleAsync(TestContext.Current.CancellationToken);
        AuditLog audit = await db.AuditLogs.SingleAsync(log => log.EntityName == nameof(SaleReturn), TestContext.Current.CancellationToken);
        Assert.Equal(actor.Id, saleReturn.ProcessedByUserId);
        Assert.Equal(actor.Id, movement.UserId);
        Assert.Equal(actor.Id, audit.UserId);
    }

    private sealed class Session(Guid actorId, string username) : ICurrentOperatorSession
    {
        public Guid OperatorId => actorId;
        public string Username => username;
        public bool IsAuthenticated => true;
    }

    private sealed class Users(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<User?>(id == user.Id ? user : null);
        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task AddAsync(User value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>([user]);
        public Task UpdateAsync(User value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class Roles : IRoleRepository
    {
        public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Role?>(new Role("Cashier"));
        public Task<Role?> GetByNameAsync(string name, CancellationToken ct = default) => Task.FromResult<Role?>(null);
        public Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Role>>([]);
        public Task<bool> HasPermissionAsync(Guid roleId, string permissionKey, CancellationToken ct = default) => Task.FromResult(permissionKey == PermissionKeys.Invoices.Return);
        public Task<IReadOnlySet<string>> GetPermissionKeysAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());
        public Task AddAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task ReplacePermissionsAsync(Guid roleId, IReadOnlySet<string> keys, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> CountUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }
}
