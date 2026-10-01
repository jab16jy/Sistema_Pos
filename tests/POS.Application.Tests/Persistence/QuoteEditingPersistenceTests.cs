using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Quotes;
using POS.Application.DTOs.Sales;
using POS.Application.UseCases.Quotes;
using POS.Application.UseCases.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Repositories;
using Xunit;

namespace POS.Application.Tests.Persistence;

/// <summary>
/// The orphan-line trap behind I4. <c>QuoteRepository.UpdateAsync</c> used to hand the whole
/// reachable graph to <c>DbSet.Update</c>, which can only touch what it can still reach: a line
/// dropped by an edit becomes unreachable, so its entry stayed Unchanged and its QUOTE_ITEMS row
/// survived. <c>Quote.Total</c> sums the in-memory collection, so the operator's total and the
/// persisted sum drifted apart permanently and silently. Everything here re-reads from a second
/// AppDbContext — and counts the raw table — because the tracked graph cannot show the bug.
/// </summary>
public sealed class QuoteEditingPersistenceTests
{
    [Fact]
    public async Task UpdateQuoteUseCase_WhenALineIsDropped_TheRowIsDeletedAndTheTotalStillAddsUp()
    {
        await using var connection = await OpenConnectionAsync();
        await using (var seedContext = CreateDbContext(connection))
        {
            await seedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        var product = new Product(
            "Widget",
            Guid.NewGuid(),
            new Money(500m),
            new Money(1_000m),
            new StockQuantity(20m),
            new StockQuantity(2m));

        Guid quoteId;
        using (var context = CreateDbContext(connection))
        {
            var quotes = new QuoteRepository(context);
            var unitOfWork = context;

            var created = await new CreateQuoteUseCase(quotes, new StubProductRepository(product), unitOfWork).ExecuteAsync(
                new CreateQuoteRequest(null, "Test Customer", null, null, 30, null,
                [
                    new CreateQuoteItemRequest(product.Id, 2m, 0m),
                    new CreateQuoteItemRequest(product.Id, 3m, 0m),
                    new CreateQuoteItemRequest(product.Id, 1m, 0m)
                ]),
                TestContext.Current.CancellationToken);
            quoteId = created.QuoteId;
        }

        await using (var editContext = CreateDbContext(connection))
        {
            var quotes = new QuoteRepository(editContext);
            var result = await new UpdateQuoteUseCase(quotes, new StubProductRepository(product), editContext, new CashierWithQuotesEditSession()).ExecuteAsync(
                new UpdateQuoteRequest(quoteId, "Ana Gomez", null, null, null, 15, null,
                [
                    // Drop the second line, re-quantity the first, and add a brand new one.
                    new CreateQuoteItemRequest(product.Id, 4m, 0m),
                    new CreateQuoteItemRequest(product.Id, 7m, 0m)
                ]),
                TestContext.Current.CancellationToken);

            Assert.Equal(11_000m, result.Total);
        }

        // Fresh context, so nothing the editing context tracked can paper over a surviving row.
        await using var reloadContext = CreateDbContext(connection);
        var reloaded = await reloadContext.Quotes
            .Include(q => q.Items)
            .SingleAsync(q => q.Id == quoteId, TestContext.Current.CancellationToken);

        Assert.Equal(2, reloaded.Items.Count);
        Assert.Equal(1_000m * 4m + 1_000m * 7m, reloaded.Total);
        Assert.Equal("Ana Gomez", reloaded.CustomerName);
        Assert.Equal(15, reloaded.ValidityDays);
        Assert.Equal(reloaded.CreatedAt.AddDays(15), reloaded.ExpiresAt);
        Assert.Equal(QuoteStatus.Draft, reloaded.Status);

        // The assertion that actually proves the orphan is gone: the raw table, not the tracked graph,
        // and scoped to this quote so it cannot pass on some other quote's rows.
        Assert.Equal(2, await CountRowsAsync(connection, "QUOTE_ITEMS", TestContext.Current.CancellationToken, quoteId));
    }

    [Fact]
    public async Task UpdateQuoteUseCase_WhenAQuoteIsEdited_KeepsOneQuoteRowAndTheOriginalNumber()
    {
        await using var connection = await OpenConnectionAsync();
        await using (var seedContext = CreateDbContext(connection))
        {
            await seedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        var product = new Product("Widget", Guid.NewGuid(), new Money(500m), new Money(1_000m), new StockQuantity(20m), new StockQuantity(2m));

        string quoteNumber;
        Guid quoteId;
        using (var context = CreateDbContext(connection))
        {
            var quotes = new QuoteRepository(context);
            var result = await new CreateQuoteUseCase(quotes, new StubProductRepository(product), context).ExecuteAsync(
                new CreateQuoteRequest(null, "Test Customer", null, null, 30, null, [new CreateQuoteItemRequest(product.Id, 1m, 0m)]),
                TestContext.Current.CancellationToken);
            quoteId = result.QuoteId;
            quoteNumber = result.QuoteNumber;
        }

        for (int edit = 1; edit <= 3; edit++)
        {
            await using var editContext = CreateDbContext(connection);
            var quotes = new QuoteRepository(editContext);
            QuoteResult result = await new UpdateQuoteUseCase(quotes, new StubProductRepository(product), editContext, new CashierWithQuotesEditSession()).ExecuteAsync(
                new UpdateQuoteRequest(quoteId, $"Customer {edit}", null, null, null, 30 + edit, null, [new CreateQuoteItemRequest(product.Id, edit, 0m)]),
                TestContext.Current.CancellationToken);
            Assert.Equal(quoteNumber, result.QuoteNumber);
        }

        await using var reloadContext = CreateDbContext(connection);
        var reloaded = await reloadContext.Quotes
            .Include(q => q.Items)
            .SingleAsync(q => q.Id == quoteId, TestContext.Current.CancellationToken);

        Assert.Equal(quoteNumber, reloaded.QuoteNumber);
        Assert.Equal("Customer 3", reloaded.CustomerName);
        Assert.Equal(33, reloaded.ValidityDays);
        // Every edit replaced the single line, so exactly one row must remain — not one per edit.
        Assert.Single(reloaded.Items);
        Assert.Equal(1, await CountRowsAsync(connection, "QUOTE_ITEMS", TestContext.Current.CancellationToken, quoteId));
        Assert.Equal(1, await CountRowsAsync(connection, "QUOTES", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancelQuoteUseCase_StillFlipsTheHeaderAndKeepsEveryLine()
    {
        // Guards the other caller of UpdateAsync: reconciling lines by Id must not start deleting
        // rows for a use case that only ever moves the quote header.
        await using var connection = await OpenConnectionAsync();
        await using (var seedContext = CreateDbContext(connection))
        {
            await seedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        var product = new Product("Widget", Guid.NewGuid(), new Money(500m), new Money(1_000m), new StockQuantity(20m), new StockQuantity(2m));

        Guid quoteId;
        using (var context = CreateDbContext(connection))
        {
            var quotes = new QuoteRepository(context);
            var created = await new CreateQuoteUseCase(quotes, new StubProductRepository(product), context).ExecuteAsync(
                new CreateQuoteRequest(null, "Test Customer", null, null, 30, null,
                [
                    new CreateQuoteItemRequest(product.Id, 1m, 0m),
                    new CreateQuoteItemRequest(product.Id, 2m, 0m)
                ]),
                TestContext.Current.CancellationToken);
            quoteId = created.QuoteId;

            await new CancelQuoteUseCase(quotes, context).ExecuteAsync(quoteId, TestContext.Current.CancellationToken);
        }

        await using var reloadContext = CreateDbContext(connection);
        var reloaded = await reloadContext.Quotes
            .Include(q => q.Items)
            .SingleAsync(q => q.Id == quoteId, TestContext.Current.CancellationToken);

        Assert.Equal(QuoteStatus.Cancelled, reloaded.Status);
        Assert.Equal(2, reloaded.Items.Count);
        Assert.Equal(2, await CountRowsAsync(connection, "QUOTE_ITEMS", TestContext.Current.CancellationToken, quoteId));
    }

    /// <summary>
    /// The other caller of the rewritten <c>UpdateAsync</c>, and the riskier one. Conversion runs
    /// <c>UpdateAsync</c> INSIDE <c>ExecuteInTransactionAsync</c> while <c>CreateSaleUseCase</c> writes
    /// through the same <see cref="AppDbContext"/>, and it does so on a quote whose <c>Items</c> were
    /// loaded with <c>.Include(q =&gt; q.Items)</c> and are therefore already tracked. That combination is
    /// exactly where the reconciliation can go wrong — a line marked Deleted or re-Added by mistake would
    /// quietly destroy the quote that the invoice was built from.
    ///
    /// <c>CommercialPresentationFlowTests</c> covers conversion against a hand-rolled repository, so it
    /// never reaches the EF reconciliation, and <c>QuoteExpirationTests</c> throws before
    /// <c>UpdateAsync</c> is called at all. This is real SQLite, a real context and the real repository.
    /// </summary>
    [Fact]
    public async Task ConvertQuoteToSaleUseCase_ThroughTheRewrittenUpdateAsync_LeavesTheQuoteConvertedAndItsLinesIntact()
    {
        await using var connection = await OpenConnectionAsync();
        await using (var seedContext = CreateDbContext(connection))
        {
            await seedContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        // Persisted first, and the SAME instance then handed to the quote builder: QUOTE_ITEMS carries an
        // FK to PRODUCTS, so building the quote lines against an unpersisted stub would fail the insert
        // for a reason that has nothing to do with the reconciliation under test.
        Guid quoteId;
        await using (var createContext = CreateDbContext(connection))
        {
            Product product = await TestProductFactory.CreateAndPersistAsync(
                createContext,
                "Widget",
                new Money(500m),
                new Money(1_000m),
                new StockQuantity(20m),
                new StockQuantity(2m),
                TestContext.Current.CancellationToken);

            var quotes = new QuoteRepository(createContext);
            QuoteResult created = await new CreateQuoteUseCase(quotes, new StubProductRepository(product), createContext).ExecuteAsync(
                new CreateQuoteRequest(null, "Test Customer", null, null, 30, null,
                [
                    new CreateQuoteItemRequest(product.Id, 2m, 0m),
                    new CreateQuoteItemRequest(product.Id, 3m, 0m)
                ]),
                TestContext.Current.CancellationToken);
            quoteId = created.QuoteId;
        }

        CreateSaleResult saleResult;
        await using (var convertContext = CreateDbContext(connection))
        {
            var createSale = new CreateSaleUseCase(
                new ProductRepository(convertContext),
                new SaleRepository(convertContext),
                convertContext,
                authorizer: TestAuthorizer.Create(dbContext: convertContext));

            saleResult = await new ConvertQuoteToSaleUseCase(new QuoteRepository(convertContext), createSale, convertContext).ExecuteAsync(
                new ConvertQuoteToSaleRequest(
                    quoteId,
                    [new CreateSalePaymentRequest(PaymentMethod.Cash, 5_000m, 5_000m, null)]),
                TestContext.Current.CancellationToken);
        }

        // Fresh context: nothing the converting context tracked can paper over a lost or duplicated row.
        await using var reloadContext = CreateDbContext(connection);
        var reloaded = await reloadContext.Quotes
            .Include(q => q.Items)
            .SingleAsync(q => q.Id == quoteId, TestContext.Current.CancellationToken);

        Assert.Equal(QuoteStatus.Converted, reloaded.Status);
        Assert.Equal(saleResult.SaleId, reloaded.ConvertedSaleId);
        // The lines the invoice was built from are still here, unchanged, and the row count did not move:
        // conversion rewrites the header only, so a reconcile that touched the lines would show up here.
        Assert.Equal(2, reloaded.Items.Count);
        Assert.Equal(1_000m * 2m + 1_000m * 3m, reloaded.Total);
        Assert.Equal(2, await CountRowsAsync(connection, "QUOTE_ITEMS", TestContext.Current.CancellationToken, quoteId));
        Assert.Equal(1, await CountRowsAsync(connection, "QUOTES", TestContext.Current.CancellationToken));
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        return connection;
    }

    private static AppDbContext CreateDbContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    /// <summary>
    /// Counts rows in a raw table, optionally scoped to one quote. Scoped rather than whole-table because
    /// a whole-table count can only pass for the right reason while each fixture seeds exactly one quote;
    /// the moment a second one appears it would report the sum of both and stop proving anything about
    /// the quote under test.
    /// </summary>
    private static async Task<int> CountRowsAsync(
        SqliteConnection connection,
        string table,
        CancellationToken ct,
        Guid? quoteId = null)
    {
        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = quoteId.HasValue
            ? $"SELECT COUNT(*) FROM \"{table}\" WHERE \"QuoteId\" = @quoteId;"
            : $"SELECT COUNT(*) FROM \"{table}\";";
        if (quoteId.HasValue)
        {
            SqliteParameter parameter = command.CreateParameter();
            parameter.ParameterName = "@quoteId";
            parameter.Value = quoteId.Value.ToString();
            command.Parameters.Add(parameter);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    /// <summary>The edit use case enforces Quotes.Edit itself, so the persistence round-trips need a session that holds it.</summary>
    private sealed class CashierWithQuotesEditSession : Application.Interfaces.ICurrentOperatorSession
    {
        public Guid OperatorId { get; } = Guid.NewGuid();
        public string Username => "cashier";
        public IReadOnlySet<string> Permissions { get; } =
            new HashSet<string>([Domain.Authorization.PermissionKeys.Quotes.Edit], StringComparer.Ordinal);
        public bool IsAuthenticated => true;
    }

    private sealed class StubProductRepository(Product product) : IProductRepository
    {
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(product.Id == id ? product : null);

        public Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
            => Task.FromResult<Product?>(product.Barcode == barcode ? product : null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}