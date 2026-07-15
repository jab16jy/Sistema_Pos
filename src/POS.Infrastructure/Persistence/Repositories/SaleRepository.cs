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

    public async Task<IReadOnlyList<Sale>> GetOnHoldByCashierAsync(
        Guid cashierUserId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sales
            .AsNoTracking()
            .Include(sale => sale.Items)
            .Where(sale => sale.CashierUserId == cashierUserId && sale.Status == SaleStatus.OnHold)
            .OrderByDescending(sale => sale.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DashboardSummaryData> GetDashboardSummaryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset utcFrom = from.ToUniversalTime();
        DateTimeOffset utcTo = to.ToUniversalTime();

        List<Sale> sales = await _dbContext.Sales
            .Include(sale => sale.Items)
            .Include(sale => sale.Payments)
            .Where(sale => sale.CreatedAt >= utcFrom && sale.CreatedAt < utcTo && sale.Status == SaleStatus.Completed)
            .ToListAsync(cancellationToken);

        int lowStockCount = await _dbContext.Products
            .CountAsync(product => product.IsActive && product.MinimumStock > 0m && product.CurrentStock <= product.MinimumStock, cancellationToken);

        decimal salesTotal = sales.Sum(sale => sale.Total().Amount);
        decimal cashInDrawer = sales.Sum(sale => sale.CashInDrawer().Amount);
        decimal digitalPayments = sales
            .SelectMany(sale => sale.Payments)
            .Where(payment => IsDigitalPayment(payment.Method))
            .Sum(payment => payment.Amount.Amount);

        return new DashboardSummaryData(
            salesTotal,
            cashInDrawer,
            digitalPayments,
            lowStockCount,
            sales.Count);
    }

    public async Task<ShiftSalesSummaryData> GetShiftSalesSummaryAsync(
        Guid cashierUserId,
        DateTimeOffset openedAt,
        CancellationToken cancellationToken = default)
        => await GetShiftSalesSummaryAsync(cashierUserId, openedAt, DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);

    public async Task<ShiftSalesSummaryData> GetShiftSalesSummaryAsync(
        Guid cashierUserId,
        DateTimeOffset openedAt,
        DateTimeOffset closedAt,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset utcOpenedAt = openedAt.ToUniversalTime();
        DateTimeOffset utcClosedAt = closedAt.ToUniversalTime();

        List<Sale> sales = await _dbContext.Sales
            .Include(sale => sale.Items)
            .Include(sale => sale.Payments)
            .Where(sale => sale.CashierUserId == cashierUserId
                && sale.CreatedAt >= utcOpenedAt
                && sale.CreatedAt < utcClosedAt
                && sale.Status == SaleStatus.Completed)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ShiftSalesSummaryData(
            sales.Sum(sale => sale.Total().Amount),
            sales.Sum(sale => sale.CashInDrawer().Amount),
            sales.SelectMany(sale => sale.Payments)
                .Where(payment => IsDigitalPayment(payment.Method))
                .Sum(payment => payment.Amount.Amount));
    }

    private static bool IsDigitalPayment(PaymentMethod method)
        => method is PaymentMethod.DebitCard or PaymentMethod.CreditCard or PaymentMethod.Transfer;
}
