using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class SaleRepository : ISaleRepository
{
    private readonly AppDbContext _dbContext;

    public SaleRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        await _dbContext.Sales.AddAsync(sale, cancellationToken);
    }

    public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Sales
            .Include(sale => sale.Items)
            .Include(sale => sale.Payments)
            .FirstOrDefaultAsync(sale => sale.Id == id, cancellationToken);
    }

    public async Task<DashboardSummaryData> GetDashboardSummaryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        List<Sale> sales = await _dbContext.Sales
            .Include(sale => sale.Items)
            .Include(sale => sale.Payments)
            .ToListAsync(cancellationToken);

        sales = sales
            .Where(sale => sale.CreatedAt >= from && sale.CreatedAt <= to && sale.Status == SaleStatus.Completed)
            .ToList();

        int lowStockCount = await _dbContext.Products
            .CountAsync(product => product.IsActive && product.CurrentStock <= product.MinimumStock, cancellationToken);

        decimal salesTotal = sales.Sum(sale => sale.Total().Amount);
        decimal cashInDrawer = sales.Sum(sale => sale.CashInDrawer().Amount);
        decimal digitalPayments = sales
            .SelectMany(sale => sale.Payments)
            .Where(payment => payment.Method != PaymentMethod.Cash)
            .Sum(payment => payment.Amount.Amount);

        return new DashboardSummaryData(
            salesTotal,
            cashInDrawer,
            digitalPayments,
            lowStockCount,
            sales.Count);
    }
}
