using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Customers;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Persistence.Queries;

public sealed class CustomerHistoryQueryService : ICustomerHistoryQueryService
{
    private readonly AppDbContext _dbContext;

    public CustomerHistoryQueryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CustomerHistoryResult> GetHistoryAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customerSales = await _dbContext.Sales
            .AsNoTracking()
            .Include(sale => sale.Items)
            .Where(sale => sale.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        var customerQuotes = await _dbContext.Quotes
            .AsNoTracking()
            .Include(quote => quote.Items)
            .Where(quote => quote.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        var receivables = await _dbContext.Receivables
            .AsNoTracking()
            .Where(receivable => receivable.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        List<CustomerHistoryEntry> entries = [
            .. customerSales.Select(sale => new CustomerHistoryEntry(
                sale.CreatedAt,
                "Sale",
                sale.InvoiceNumber,
                sale.Items.Sum(item => (item.UnitPrice.Amount * item.Quantity) - item.Discount.Amount),
                sale.Status.ToString(),
                sale.Id)),
            .. customerQuotes.Select(quote => new CustomerHistoryEntry(
                new DateTimeOffset(DateTime.SpecifyKind(quote.CreatedAt, DateTimeKind.Utc)),
                "Quote",
                quote.QuoteNumber,
                quote.Total,
                quote.Status.ToString())),
            .. receivables.Select(receivable => new CustomerHistoryEntry(
                receivable.CreatedAt,
                "Receivable",
                $"Saldo venta {receivable.SaleId:N}",
                receivable.OutstandingBalance.Amount,
                receivable.IsCancelled ? "Cancelled" : receivable.IsPaidOff ? "Paid" : "Pending"))];

        entries = [.. entries.OrderByDescending(entry => entry.OccurredAt)];

        decimal outstandingBalance = receivables.Sum(receivable => receivable.OutstandingBalance.Amount);

        return new CustomerHistoryResult(customerId, entries, outstandingBalance);
    }
}
