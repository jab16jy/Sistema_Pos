using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Invoices;
using POS.Application.Interfaces;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Queries;

public sealed class SalesHistoryQueryService : ISalesHistoryQueryService
{
    private readonly AppDbContext _dbContext;

    public SalesHistoryQueryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SaleHistoryResult>> SearchAsync(
        SalesHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 200);
        string? searchText = string.IsNullOrWhiteSpace(request.SearchText)
            ? null
            : request.SearchText.Trim();

        var query =
            from sale in _dbContext.Sales.AsNoTracking()
            join customer in _dbContext.Customers.AsNoTracking()
                on sale.CustomerId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            select new { Sale = sale, Customer = customer };

        if (request.From.HasValue)
        {
            DateTimeOffset from = new(request.From.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(row => row.Sale.CreatedAt >= from);
        }

        if (request.To.HasValue)
        {
            DateTimeOffset to = new(request.To.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            query = query.Where(row => row.Sale.CreatedAt <= to);
        }

        if (searchText is not null)
        {
            string pattern = $"%{searchText}%";
            query = query.Where(row =>
                row.Sale.InvoiceNumber == searchText ||
                (row.Customer != null && EF.Functions.Like(row.Customer.FullName, pattern)));
        }

        var pageRows = await query
            .OrderByDescending(row => row.Sale.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new
            {
                row.Sale.Id,
                row.Sale.InvoiceNumber,
                row.Sale.CustomerId,
                CustomerName = row.Customer == null ? null : row.Customer.FullName,
                row.Sale.Status,
                row.Sale.CreatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Totals computed from flat scalar columns fetched separately, then summed client-side.
        // EF Core/SQLite cannot translate a Sum over money-converted columns once it's combined
        // with a GroupBy or a navigation (Sale.Items) inside a Select — both attempts threw
        // "could not be translated" — so the arithmetic is done in memory instead.
        List<Guid> saleIds = pageRows.Select(row => row.Id).ToList();
        var itemAmounts = await _dbContext.Set<SaleItem>()
            .AsNoTracking()
            .Where(item => saleIds.Contains(item.SaleId))
            .Select(item => new { item.SaleId, UnitPrice = item.UnitPrice.Amount, item.Quantity, Discount = item.Discount.Amount })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, decimal> totals = itemAmounts
            .GroupBy(item => item.SaleId)
            .ToDictionary(g => g.Key, g => g.Sum(item => item.UnitPrice * item.Quantity - item.Discount));

        return pageRows
            .Select(row => new SaleHistoryResult(
                row.Id,
                row.InvoiceNumber,
                row.CustomerId,
                row.CustomerName,
                row.Status,
                row.CreatedAt,
                totals.TryGetValue(row.Id, out decimal total) ? total : 0m))
            .ToList();
    }
}
