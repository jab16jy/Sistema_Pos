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
            join user in _dbContext.Users.AsNoTracking()
                on sale.CashierUserId equals user.Id into cashiers
            from cashier in cashiers.DefaultIfEmpty()
            select new { Sale = sale, Customer = customer, Cashier = cashier };

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
                CashierName = row.Cashier == null ? null : row.Cashier.Username,
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
                totals.TryGetValue(row.Id, out decimal total) ? total : 0m,
                row.CashierName))
            .ToList();
    }

    public async Task<InvoiceDetailResult?> GetDetailAsync(
        Guid saleId,
        CancellationToken cancellationToken = default)
    {
        var saleRow = await (
            from sale in _dbContext.Sales.AsNoTracking()
            join customer in _dbContext.Customers.AsNoTracking()
                on sale.CustomerId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            join user in _dbContext.Users.AsNoTracking()
                on sale.CashierUserId equals user.Id into cashiers
            from cashier in cashiers.DefaultIfEmpty()
            where sale.Id == saleId
            select new
            {
                sale.Id,
                sale.InvoiceNumber,
                sale.CustomerId,
                CustomerName = customer == null ? null : customer.FullName,
                CashierName = cashier == null ? null : cashier.Username,
                sale.Status,
                sale.CreatedAt
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (saleRow is null)
        {
            return null;
        }

        var items = await _dbContext.Set<SaleItem>()
            .AsNoTracking()
            .Where(item => item.SaleId == saleId)
            .OrderBy(item => item.Id)
            .Select(item => new InvoiceDetailItemResult(
                item.Id,
                item.ProductId,
                item.ProductName,
                item.Quantity,
                item.UnitPrice.Amount,
                item.Discount.Amount,
                (item.UnitPrice.Amount * item.Quantity) - item.Discount.Amount,
                item.Presentation,
                item.UnitsPerPresentation))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var paymentRows = await _dbContext.Set<Payment>()
            .AsNoTracking()
            .Where(payment => payment.SaleId == saleId)
            .OrderBy(payment => payment.Id)
            .Select(payment => new
            {
                payment.Id,
                payment.Method,
                Amount = payment.Amount.Amount,
                ReceivedAmount = payment.ReceivedAmount == null ? null : (decimal?)payment.ReceivedAmount.Value.Amount,
                payment.BankOrPlatform
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<InvoicePaymentResult> payments = paymentRows
            .Select(payment => new InvoicePaymentResult(
                payment.Id,
                payment.Method,
                payment.Amount,
                payment.ReceivedAmount,
                payment.ReceivedAmount is null ? 0m : Math.Max(0m, payment.ReceivedAmount.Value - payment.Amount),
                payment.BankOrPlatform))
            .ToList();

        decimal total = items.Sum(item => item.Subtotal);

        var receivable = await _dbContext.Receivables
            .AsNoTracking()
            .Include(r => r.Settlements)
            .SingleOrDefaultAsync(r => r.SaleId == saleId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<InvoiceSettlementResult>? settlements = receivable?.Settlements
            .OrderBy(settlement => settlement.AppliedAt)
            .Select(settlement => new InvoiceSettlementResult(
                settlement.Id,
                settlement.Method,
                settlement.Amount.Amount,
                settlement.AppliedAt,
                settlement.BankOrPlatform))
            .ToList();

        return new InvoiceDetailResult(
            saleRow.Id,
            saleRow.InvoiceNumber,
            saleRow.CustomerId,
            saleRow.CustomerName,
            saleRow.Status,
            saleRow.CreatedAt,
            total,
            saleRow.CashierName,
            items,
            payments,
            receivable?.OutstandingBalance.Amount ?? 0m,
            settlements);
    }
}
