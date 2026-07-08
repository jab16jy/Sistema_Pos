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
        var count = await _dbContext.Quotes.CountAsync(ct);
        return $"COT-{DateTime.UtcNow.Year}-{(count + 1):D4}";
    }

    public async Task AddAsync(Quote quote, CancellationToken ct = default)
    {
        await _dbContext.Quotes.AddAsync(quote, ct);
    }

    public Task UpdateAsync(Quote quote, CancellationToken ct = default)
    {
        _dbContext.Quotes.Update(quote);
        return Task.CompletedTask;
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
