using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class QuoteRepository : IQuoteRepository
{
    private readonly AppDbContext _dbContext;

    public QuoteRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Quote?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _dbContext.Quotes
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == id, ct);
    }

    public async Task<IReadOnlyList<Quote>> GetOpenByCustomerIdAsync(Guid customerId, CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;

        return await _dbContext.Quotes
            .Include(q => q.Items)
            .Where(q => q.CustomerId == customerId)
            .Where(q => q.Status == QuoteStatus.Draft || q.Status == QuoteStatus.Active)
            .Where(q => q.ExpiresAt >= now)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<string> GetNextQuoteNumberAsync(CancellationToken ct = default)
    {
        int year = DateTime.UtcNow.Year;
        string prefix = $"COT-{year}-";

        // ponytail: single atomic UPSERT/RETURNING reservation (must run inside the caller's
        // transaction). Seeds LastIssued from the highest valid "COT-{year}-####" number already
        // in QUOTES so migrating a populated database never collides; after the first row exists
        // for a year it simply increments. Deletion never touches this row, so numbers are burned,
        // not reused. Not generalized into a shared sequence service (out of scope for this packet).
        DbConnection connection = _dbContext.Database.GetDbConnection();
        bool closeConnection = connection.State != System.Data.ConnectionState.Open;
        if (closeConnection)
        {
            await _dbContext.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        }

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO "QUOTE_SEQUENCES" ("Year", "LastIssued")
                VALUES (
                    @year,
                    1 + COALESCE(
                        (SELECT MAX(CAST(SUBSTR("QuoteNumber", LENGTH(@prefix) + 1) AS INTEGER))
                         FROM "QUOTES"
                         WHERE "QuoteNumber" LIKE @likePrefix ESCAPE '\'),
                        0))
                ON CONFLICT("Year") DO UPDATE SET "LastIssued" = "LastIssued" + 1
                RETURNING "LastIssued";
                """;

            DbParameter yearParam = command.CreateParameter();
            yearParam.ParameterName = "@year";
            yearParam.Value = year;
            command.Parameters.Add(yearParam);

            DbParameter prefixParam = command.CreateParameter();
            prefixParam.ParameterName = "@prefix";
            prefixParam.Value = prefix;
            command.Parameters.Add(prefixParam);

            DbParameter likePrefixParam = command.CreateParameter();
            likePrefixParam.ParameterName = "@likePrefix";
            likePrefixParam.Value = prefix.Replace("_", "\\_").Replace("%", "\\%") + "%";
            command.Parameters.Add(likePrefixParam);

            long issued = Convert.ToInt64(await command.ExecuteScalarAsync(ct).ConfigureAwait(false));
            return $"{prefix}{issued:D4}";
        }
        finally
        {
            if (closeConnection)
            {
                await _dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task AddAsync(Quote quote, CancellationToken ct = default)
    {
        await _dbContext.Quotes.AddAsync(quote, ct);
    }

    /// <summary>
    /// Persists a quote header plus whatever line collection the aggregate now holds.
    ///
    /// This does NOT use <c>DbSet.Update</c>. That stamps Modified on every <em>reachable</em>
    /// entity and nothing else, which is wrong in both directions once a quote becomes editable:
    /// a line dropped by <c>ReplaceItems</c> is no longer reachable, so its entry stays Unchanged and
    /// its QUOTE_ITEMS row survives as an orphan that no longer contributes to <c>Quote.Total</c> —
    /// the operator's total and the persisted sum then disagree for good (I4). And a line added by
    /// <c>ReplaceItems</c> carries a fresh Id, so Modified would be sent as an UPDATE against a row
    /// that does not exist yet.
    ///
    /// Reconciling the lines by Id leaves the two existing callers unchanged in behaviour:
    /// <c>CancelQuoteUseCase</c> and <c>ConvertQuoteToSaleUseCase</c> only ever move the header, and
    /// the lines they keep are immutable, so rewriting those rows was pure noise.
    /// </summary>
    public async Task UpdateAsync(Quote quote, CancellationToken ct = default)
    {
        // Ask the database which lines it holds instead of reading the tracked graph. EF's
        // relationship fixup drops a line out of the quote's Items navigation whenever it cannot pair
        // it by QuoteId, and a line ReplaceItems just built starts with an empty Guid — so anything
        // that triggers DetectChanges before those lines are tracked as Added wipes the collection
        // the aggregate sums in Total. A no-tracking query touches neither.
        var persistedItemIds = await _dbContext.QuoteItems
            .AsNoTracking()
            .Where(item => item.QuoteId == quote.Id)
            .Select(item => item.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var keptItemIds = quote.Items.Select(item => item.Id).ToHashSet();
        var persistedIdSet = persistedItemIds.ToHashSet();

        bool autoDetectChanges = _dbContext.ChangeTracker.AutoDetectChangesEnabled;

        try
        {
            // Off for the whole reconciliation: ChangeTracker.Entries() runs DetectChanges, and
            // against a half-swapped collection that empties Items and leaves nothing to reconcile.
            _dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

            // Header first. Marking a tracked principal Modified cascades through its Items
            // navigation, so the line states have to be asserted afterwards regardless of whether it
            // actually reaches them — and the added lines must be tracked after it, or the cascade
            // would rewrite them from Added to Modified.
            _dbContext.Entry(quote).State = EntityState.Modified;

            foreach (var entry in _dbContext.ChangeTracker.Entries<QuoteItem>().ToList())
            {
                if (entry.Entity.QuoteId == quote.Id)
                {
                    // A line this quote had before. A surviving line is immutable (QuoteItem has no
                    // mutator), so it needs no write; a dropped one has to be deleted explicitly,
                    // because Update cannot reach an entity the collection no longer holds. If a
                    // mutator is ever added to QuoteItem, this is the line that would silently stop
                    // persisting that change.
                    entry.State = keptItemIds.Contains(entry.Entity.Id) ? EntityState.Unchanged : EntityState.Deleted;
                }
            }

            foreach (QuoteItem item in quote.Items)
            {
                if (!persistedIdSet.Contains(item.Id))
                {
                    // A brand new line: an INSERT. Matching on persistedItemIds rather than QuoteId
                    // keeps a line of some other quote out of here, and skips the surviving ones —
                    // re-adding a tracked line would INSERT it again and break on QUOTE_ITEMS.Id.
                    // EF fills in QuoteId from the quote's Items navigation once, at SaveChanges.
                    _dbContext.QuoteItems.Add(item);
                }
            }
        }
        finally
        {
            _dbContext.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
    }

    public async Task<(IReadOnlyList<Quote> Items, int Total)> GetHistoryAsync(
        DateOnly? from,
        DateOnly? to,
        string? customerName,
        QuoteStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var inactiveCustomerIds = _dbContext.Customers
            .AsNoTracking()
            .Where(c => !c.IsActive)
            .Select(c => c.Id);

        var query = _dbContext.Quotes
            .Include(q => q.Items)
            .Where(q => q.CustomerId == null || q.Status == QuoteStatus.Converted || !inactiveCustomerIds.Contains(q.CustomerId.Value))
            .AsQueryable();

        if (from.HasValue)
        {
            var fromDate = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(q => q.CreatedAt >= fromDate);
        }

        if (to.HasValue)
        {
            var toDate = to.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(q => q.CreatedAt <= toDate);
        }

        if (!string.IsNullOrWhiteSpace(customerName))
        {
            query = query.Where(q => q.CustomerName.Contains(customerName));
        }

        if (status.HasValue)
        {
            query = query.Where(q => q.Status == status.Value);
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
