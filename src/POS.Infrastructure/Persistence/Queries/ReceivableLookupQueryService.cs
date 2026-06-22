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
        // OutstandingBalance is a Money value object — EF converts it to decimal for storage.
        // We filter on the mapped column via the Amount property.
        var query = _dbContext.Receivables
            .Where(r => r.OutstandingBalance.Amount > 0m);

        if (customerId.HasValue)
        {
            query = query.Where(r => r.CustomerId == customerId.Value);
        }

        var receivables = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

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
