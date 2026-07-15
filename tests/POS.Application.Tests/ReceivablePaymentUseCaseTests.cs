using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Loyalty;
using POS.Application.Interfaces;
using POS.Application.UseCases.Loyalty;
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

    [Fact]
    public async Task ExecuteAsync_WithActiveLoyaltyRule_AwardsPointsForSettledAmount()
    {
        var customerId = Guid.NewGuid();
        var saleId = Guid.NewGuid();
        var receivable = new Receivable(customerId, saleId, new Money(10_000m));
        var repository = new InMemoryReceivableRepository([receivable]);
        var unitOfWork = new RecordingUnitOfWork();
        var loyalty = new InMemoryLoyaltyRepository { ActiveRule = new LoyaltyRule(0.001m, 1m, 0) };
        var accumulatePoints = new AccumulateLoyaltyPointsUseCase(loyalty, unitOfWork);
        var useCase = new ApplyReceivablePaymentUseCase(repository, unitOfWork, accumulatePoints);

        await useCase.ExecuteAsync(
            receivable.Id,
            4_000m,
            PaymentMethod.Cash,
            null,
            TestContext.Current.CancellationToken);

        Assert.Single(loyalty.Transactions);
        Assert.Equal(4, loyalty.Transactions[0].Points); // FLOOR(4000 * 0.001)
        Assert.Equal(1, unitOfWork.SaveCount); // points ride the same SaveChanges as the payment
    }

    [Fact]
    public async Task ExecuteAllAsync_WithActiveLoyaltyRule_AwardsPointsPerReceivableSettledAmount()
    {
        var customerId = Guid.NewGuid();
        var first = new Receivable(customerId, Guid.NewGuid(), new Money(12_000m));
        var second = new Receivable(customerId, Guid.NewGuid(), new Money(8_000m));
        var repository = new InMemoryReceivableRepository([first, second]);
        var unitOfWork = new RecordingUnitOfWork();
        var loyalty = new InMemoryLoyaltyRepository { ActiveRule = new LoyaltyRule(0.001m, 1m, 0) };
        var accumulatePoints = new AccumulateLoyaltyPointsUseCase(loyalty, unitOfWork);
        var useCase = new ApplyReceivablePaymentUseCase(repository, unitOfWork, accumulatePoints);

        decimal amountApplied = await useCase.ExecuteAllAsync(
            customerId,
            PaymentMethod.Cash,
            null,
            TestContext.Current.CancellationToken);

        Assert.Equal(20_000m, amountApplied);
        Assert.Equal(2, loyalty.Transactions.Count);
        Assert.Contains(loyalty.Transactions, transaction => transaction.Points == 12); // FLOOR(12000 * 0.001)
        Assert.Contains(loyalty.Transactions, transaction => transaction.Points == 8);  // FLOOR(8000 * 0.001)
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class InMemoryLoyaltyRepository : ILoyaltyRepository
    {
        public LoyaltyRule? ActiveRule { get; set; }
        public List<LoyaltyAccount> Accounts { get; } = [];
        public List<LoyaltyTransaction> Transactions { get; } = [];
        public List<LoyaltyTier> Tiers { get; } = [];

        public Task<LoyaltyAccount?> GetAccountByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(Accounts.FirstOrDefault(account => account.CustomerId == customerId));

        public Task AddAccountAsync(LoyaltyAccount account, CancellationToken cancellationToken = default)
        {
            Accounts.Add(account);
            return Task.CompletedTask;
        }

        public Task AddTransactionAsync(LoyaltyTransaction transaction, CancellationToken cancellationToken = default)
        {
            Transactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LoyaltyTransaction>> GetTransactionsAsync(Guid loyaltyAccountId, int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(Transactions.Where(transaction => transaction.LoyaltyAccountId == loyaltyAccountId).Take(maxCount).ToList());

        public Task<LoyaltyRule?> GetActiveRuleAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ActiveRule);

        public Task<IReadOnlyList<LoyaltyRule>> GetRulesAsync(int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyRule>>(ActiveRule is null ? [] : [ActiveRule]);

        public Task AddRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default)
        {
            ActiveRule = rule;
            return Task.CompletedTask;
        }

        public Task UpdateRuleAsync(LoyaltyRule rule, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> GetBalanceAsync(Guid loyaltyAccountId, CancellationToken cancellationToken = default)
            => Task.FromResult(Transactions.Where(transaction => transaction.LoyaltyAccountId == loyaltyAccountId).Sum(transaction => transaction.Points));

        public Task<IReadOnlyList<LoyaltyTier>> GetTiersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTier>>(Tiers);

        public Task<int> GetTotalIssuedPointsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Transactions.Where(transaction => transaction.Points > 0).Sum(transaction => transaction.Points));

        public Task<int> GetEnrolledCustomerCountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Accounts.Count);

        public Task<int> GetRedemptionCountAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
            => Task.FromResult(Transactions.Count(transaction => transaction.Points < 0));

        public Task<IReadOnlyList<LoyaltyTransaction>> GetRecentProgramTransactionsAsync(int maxCount = 20, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LoyaltyTransaction>>(Transactions.Take(maxCount).ToList());
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
