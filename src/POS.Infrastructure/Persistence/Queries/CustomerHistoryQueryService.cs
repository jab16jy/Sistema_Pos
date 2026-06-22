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

        List<CustomerHistoryEntry> sales = [.. customerSales
            .OrderByDescending(sale => sale.CreatedAt)
            .Select(sale => new CustomerHistoryEntry(
                sale.CreatedAt,
                "Sale",
                sale.InvoiceNumber,
                sale.Items.Sum(item => (item.UnitPrice.Amount * item.Quantity) - item.Discount.Amount),
                sale.Status.ToString()))];

        return new CustomerHistoryResult(customerId, sales);
    }
}
