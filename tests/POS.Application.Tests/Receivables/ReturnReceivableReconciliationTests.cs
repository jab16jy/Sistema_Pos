using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Invoices;
using POS.Application.UseCases.Invoices;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests.Receivables;

public sealed class ReturnReceivableReconciliationTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCommitFails_RollsBackReturnStockAndResolution()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var db = CreateDbContext(connection);
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var customer = new Customer("Credit customer", "credit-customer");
        var category = new Category("Returns");
        var product = new Product("Product", category.Id, new Money(5m), new Money(10m), new StockQuantity(10m), new StockQuantity(0m));
        db.AddRange(customer, category, product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var cashierId = Guid.NewGuid();
        var authorizer = TestAuthorizer.Create(cashierId, db);

        var sale = new Sale(cashierId, customer.Id);
        sale.AddItem(product, new Quantity(1m), Money.Zero);
        sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(10m), new Money(10m)));
        sale.Complete();
        var receivable = new Receivable(customer.Id, sale.Id, new Money(10m));
        db.AddRange(sale, receivable);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var unitOfWork = new ThrowingSaveUnitOfWork(db);
        var useCase = new ProcessReturnUseCase(
            new SaleRepository(db), new ProductRepository(db), new InventoryRepository(db), new SaleReturnRepository(db), unitOfWork,
            new Users(), receivables: new ReceivableRepository(db), authorizer: authorizer);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new ProcessReturnRequest(
            sale.Id, "Rollback", Guid.NewGuid(), ReturnResolutionChoice.Receivable, [new(sale.Items.Single().Id, 1)]), TestContext.Current.CancellationToken));

        Assert.Equal(1, unitOfWork.TransactionCount);
        await using var verification = CreateDbContext(connection);
        Assert.Empty(await verification.SaleReturns.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await verification.InventoryMovements.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await verification.ReturnSettlements.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await verification.ReturnReceivableReconciliations.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(9m, (await verification.Products.SingleAsync(TestContext.Current.CancellationToken)).CurrentStock);
        Assert.Equal(10m, (await verification.Receivables.SingleAsync(TestContext.Current.CancellationToken)).OutstandingBalance.Amount);
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    private sealed class ThrowingSaveUnitOfWork(AppDbContext db) : IUnitOfWork
    {
        public int TransactionCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Persistence failure.");
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken = default)
        {
            TransactionCount++;
            return db.ExecuteInTransactionAsync(operation, cancellationToken);
        }
    }

    private sealed class Users : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>([]);
        public Task UpdateAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CountActiveUsersInRoleAsync(Guid roleId, CancellationToken ct = default) => Task.FromResult(0);
    }
}
