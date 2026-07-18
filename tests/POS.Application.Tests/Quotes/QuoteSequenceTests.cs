using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests.Quotes;

public sealed class QuoteSequenceTests
{
    [Fact]
    public async Task GetNextQuoteNumberAsync_ConcurrentSameYear_IssuesDistinctNumbers()
    {
        await using var connection = await OpenConnectionAsync();
        await using var initContext = CreateDbContext(connection);
        await initContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await using var dbContext1 = CreateDbContext(connection);
        await using var dbContext2 = CreateDbContext(connection);
        var repo1 = new QuoteRepository(dbContext1);
        var repo2 = new QuoteRepository(dbContext2);

        // ponytail: SQLite serializes writers at the connection/transaction level, so "concurrent"
        // here means both reservations race for the same shared-cache connection without either
        // depending on a prior COUNT(*) read; the immediate-transaction UPSERT/RETURNING is what
        // guarantees distinctness, matching the pattern in ProductConcurrencyTests.
        string number1 = null!;
        string number2 = null!;
        var task1 = dbContext1.ExecuteInTransactionAsync(async ct => number1 = await repo1.GetNextQuoteNumberAsync(ct), TestContext.Current.CancellationToken);
        var task2 = dbContext2.ExecuteInTransactionAsync(async ct => number2 = await repo2.GetNextQuoteNumberAsync(ct), TestContext.Current.CancellationToken);
        await Task.WhenAll(task1, task2);

        Assert.NotEqual(number1, number2);
        int year = DateTime.UtcNow.Year;
        Assert.Contains($"COT-{year}-", number1);
        Assert.Contains($"COT-{year}-", number2);
    }

    [Fact]
    public async Task GetNextQuoteNumberAsync_FirstAllocation_SeedsFromHighestHistoricalNumber()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        int year = DateTime.UtcNow.Year;
        dbContext.Quotes.Add(new Quote($"COT-{year}-0125", "Legacy Customer", 30));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new QuoteRepository(dbContext);
        string next = null!;
        await dbContext.ExecuteInTransactionAsync(async ct => next = await repository.GetNextQuoteNumberAsync(ct), TestContext.Current.CancellationToken);

        Assert.Equal($"COT-{year}-0126", next);
    }

    [Fact]
    public async Task GetNextQuoteNumberAsync_AfterQuoteDeletion_DoesNotReuseNumber()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var repository = new QuoteRepository(dbContext);

        string first = null!;
        await dbContext.ExecuteInTransactionAsync(async ct => first = await repository.GetNextQuoteNumberAsync(ct), TestContext.Current.CancellationToken);

        var quote = new Quote(first, "Customer", 30);
        dbContext.Quotes.Add(quote);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Deleting the issued quote must not free/decrement its number: the sequence row is the
        // source of truth, not a COUNT(*) over surviving QUOTES rows.
        dbContext.Quotes.Remove(quote);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        string second = null!;
        await dbContext.ExecuteInTransactionAsync(async ct => second = await repository.GetNextQuoteNumberAsync(ct), TestContext.Current.CancellationToken);

        Assert.NotEqual(first, second);
        int firstSeq = int.Parse(first.Split('-')[^1]);
        int secondSeq = int.Parse(second.Split('-')[^1]);
        Assert.True(secondSeq > firstSeq);
    }

    [Fact]
    public async Task GetNextQuoteNumberAsync_NewCalendarYear_StartsIndependentCounter()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = CreateDbContext(connection);
        await dbContext.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        dbContext.QuoteSequences.Add(new QuoteSequence(2026, 42));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var repository = new QuoteRepository(dbContext);
        string next2027 = null!;
        await dbContext.ExecuteInTransactionAsync(async ct => next2027 = await repository.GetNextQuoteNumberAsync(ct), TestContext.Current.CancellationToken);

        int currentYear = DateTime.UtcNow.Year;
        // This test exercises the current year's independence from a pre-seeded, unrelated 2026 row;
        // when run in 2026 itself, seed a distinct future year instead so the two rows never collide.
        if (currentYear == 2026)
        {
            dbContext.QuoteSequences.Remove(await dbContext.QuoteSequences.SingleAsync(s => s.Year == 2026, TestContext.Current.CancellationToken));
            dbContext.QuoteSequences.Add(new QuoteSequence(2099, 42));
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

            await dbContext.ExecuteInTransactionAsync(async ct => next2027 = await repository.GetNextQuoteNumberAsync(ct), TestContext.Current.CancellationToken);
            Assert.Equal($"COT-{currentYear}-0001", next2027);

            var untouched2099 = await dbContext.QuoteSequences.SingleAsync(s => s.Year == 2099, TestContext.Current.CancellationToken);
            Assert.Equal(42, untouched2099.LastIssued);
            return;
        }

        Assert.Equal($"COT-{currentYear}-0001", next2027);
        var untouched2026 = await dbContext.QuoteSequences.SingleAsync(s => s.Year == 2026, TestContext.Current.CancellationToken);
        Assert.Equal(42, untouched2026.LastIssued);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
}
