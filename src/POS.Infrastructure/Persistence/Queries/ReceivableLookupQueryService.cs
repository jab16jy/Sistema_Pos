using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Receivables;
using POS.Application.Interfaces;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Persistence.Queries;

/// <summary>
/// EF-backed query service for open customer receivables.
/// Joins Receivable → Sale (for invoice number) → Customer (for name).
/// </summary>
public sealed class ReceivableLookupQueryService : IReceivableQueryService
{
    private readonly AppDbContext _dbContext;

    public ReceivableLookupQueryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ReceivableSummary>> GetOpenReceivablesAsync(
        Guid? customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // CustomerId is a plain Guid column, so scope by it server-side.
        var query = _dbContext.Receivables.AsNoTracking();
        if (customerId.HasValue)
        {
            query = query.Where(r => r.CustomerId == customerId.Value);
        }

        // OutstandingBalance is a Money value object — SQLite cannot translate a filter/paging over the
        // converted column (same limitation the reporting queries hit), so materialize the customer-scoped
        // rows and filter + order + page in memory.
        var receivables = (await query.ToListAsync(cancellationToken))
            .Where(r => r.OutstandingBalance.Amount > 0m)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Collect all unique sale and customer ids for denormalization
        var saleIds = receivables.Select(r => r.SaleId).Distinct().ToList();
        var customerIds = receivables.Select(r => r.CustomerId).Distinct().ToList();

        var sales = await _dbContext.Sales
            .Where(s => saleIds.Contains(s.Id))
            .Select(s => new { s.Id, s.InvoiceNumber })
            .ToListAsync(cancellationToken);

        var customers = await _dbContext.Customers
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.FullName })
            .ToListAsync(cancellationToken);

        var saleMap = sales.ToDictionary(s => s.Id, s => s.InvoiceNumber);
        var customerMap = customers.ToDictionary(c => c.Id, c => c.FullName);

        return receivables.Select(r => new ReceivableSummary(
            r.Id,
            r.CustomerId,
            customerMap.GetValueOrDefault(r.CustomerId, "—"),
            r.SaleId,
            saleMap.GetValueOrDefault(r.SaleId, "—"),
            r.OriginalBalance.Amount,
            r.OutstandingBalance.Amount,
            r.CreatedAt)).ToList();
    }
}
