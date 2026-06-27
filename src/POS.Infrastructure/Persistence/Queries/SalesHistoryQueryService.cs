using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Invoices;
using POS.Application.Interfaces;

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

        return await query
            .OrderByDescending(row => row.Sale.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new SaleHistoryResult(
                row.Sale.Id,
                row.Sale.InvoiceNumber,
                row.Sale.CustomerId,
                row.Customer == null ? null : row.Customer.FullName,
                row.Sale.Status,
                row.Sale.CreatedAt,
                row.Sale.Items.Sum(item => item.UnitPrice.Amount * item.Quantity - item.Discount.Amount)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
