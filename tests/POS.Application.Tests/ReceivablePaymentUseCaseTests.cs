using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.Interfaces;
using POS.Application.UseCases.Receivables;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests;

public sealed class ReceivablePaymentUseCaseTests
{
    [Fact]
    public async Task ExecuteAllAsync_AppliesCustomerBalanceAcrossAllOpenReceivables()
    {
        var customerId = Guid.NewGuid();
        var first = new Receivable(customerId, Guid.NewGuid(), new Money(12_000m));
        var second = new Receivable(customerId, Guid.NewGuid(), new Money(8_000m));
        var repository = new InMemoryReceivableRepository([first, second]);
        var unitOfWork = new RecordingUnitOfWork();
        var useCase = new ApplyReceivablePaymentUseCase(repository, unitOfWork);

        decimal amountApplied = await useCase.ExecuteAllAsync(
            customerId,
            PaymentMethod.Cash,
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(20_000m, amountApplied);
        Assert.True(first.IsPaidOff);
        Assert.True(second.IsPaidOff);
        Assert.Single(first.Settlements);
        Assert.Single(second.Settlements);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithSqlite_UpdatesReceivableWithoutConcurrencyConflict()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var receivable = new Receivable(Guid.NewGuid(), Guid.NewGuid(), new Money(20_000m));
        var repository = new ReceivableRepository(dbContext);
        await repository.AddAsync(receivable, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, (await dbContext.Receivables.AsNoTracking().SingleAsync(
            item => item.Id == receivable.Id,
            TestContext.Current.CancellationToken)).Version);

        var useCase = new ApplyReceivablePaymentUseCase(repository, dbContext);
        await useCase.ExecuteAsync(
            receivable.Id,
            20_000m,
            PaymentMethod.Cash,
            null,
            TestContext.Current.CancellationToken);

        Receivable persisted = await dbContext.Receivables.SingleAsync(
            item => item.Id == receivable.Id,
            TestContext.Current.CancellationToken);
        Assert.True(persisted.IsPaidOff);
    }

    private sealed class InMemoryReceivableRepository(IReadOnlyList<Receivable> receivables) : IReceivableRepository
    {
        public Task AddAsync(Receivable receivable, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Receivable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(receivables.FirstOrDefault(receivable => receivable.Id == id));

        public Task<IReadOnlyList<Receivable>> GetOpenByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Receivable>>(
                receivables.Where(receivable => receivable.CustomerId == customerId && receivable.OutstandingBalance.Amount > 0m).ToList());

        public Task<Receivable?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default)
            => Task.FromResult(receivables.FirstOrDefault(receivable => receivable.SaleId == saleId));

        public Task UpdateAsync(Receivable receivable, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }
}
