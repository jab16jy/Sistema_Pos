using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Reporting;
using POS.Application.Interfaces;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Queries;

public sealed class ProfitQueryService : IProfitQueryService
{
    private readonly AppDbContext _dbContext;

    public ProfitQueryService(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<ProductMarginResult>> GetMarginByProductAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var items = await CompletedSaleItemsAsync(from, to, cancellationToken).ConfigureAwait(false);

        return items
            .GroupBy(si => (si.ProductId, si.ProductName))
            .Select(g =>
            {
                decimal totalRevenue = g.Sum(si => (si.UnitPrice.Amount * si.Quantity) - si.Discount.Amount);
                decimal totalCost = g.Sum(si => si.UnitCost.Amount * si.Quantity);
                decimal margin = totalRevenue - totalCost;
                decimal marginPercent = totalRevenue > 0 ? margin / totalRevenue * 100m : 0m;
                return new ProductMarginResult(g.Key.ProductId, g.Key.ProductName, totalRevenue, totalCost, margin, marginPercent);
            })
            .OrderByDescending(x => x.GrossMargin)
            .ToList();
    }

    public async Task<IReadOnlyList<TopSellerResult>> GetTopSellersAsync(
        DateOnly from, DateOnly to, int top = 10, CancellationToken cancellationToken = default)
    {
        var (fromOffset, toOffset) = ToRange(from, to);

        var sales = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .Where(s => s.CreatedAt >= fromOffset && s.CreatedAt <= toOffset)
            .Include(s => s.Items)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var flatItems = sales.SelectMany(s => s.Items.Select(si => new
        {
            Item = si,
            SaleId = s.Id,
            CustomerId = s.CustomerId
        }));

        return flatItems
            .GroupBy(x => (x.Item.ProductId, x.Item.ProductName))
            .Select(g =>
            {
                var uniqueSalesCount = g.Select(x => x.SaleId).Distinct().Count();
                var registeredCustomersCount = g.Where(x => x.CustomerId.HasValue).Select(x => x.CustomerId!.Value).Distinct().Count();
                var anonymousPurchasesCount = g.Where(x => !x.CustomerId.HasValue).Select(x => x.SaleId).Distinct().Count();
                var totalBuyersCount = registeredCustomersCount + anonymousPurchasesCount;

                return new TopSellerResult(
                    g.Key.ProductId,
                    g.Key.ProductName,
                    (int)g.Sum(x => x.Item.Quantity),
                    g.Sum(x => (x.Item.UnitPrice.Amount * x.Item.Quantity) - x.Item.Discount.Amount),
                    uniqueSalesCount,
                    totalBuyersCount
                );
            })
            .OrderByDescending(r => r.UnitsSold)
            .Take(top)
            .ToList();
    }

    public async Task<PeriodComparisonResult> GetPeriodComparisonAsync(
        DateOnly currentFrom, DateOnly currentTo,
        DateOnly priorFrom, DateOnly priorTo,
        CancellationToken cancellationToken = default)
    {
        var currentItems = await CompletedSaleItemsAsync(currentFrom, currentTo, cancellationToken).ConfigureAwait(false);
        var priorItems = await CompletedSaleItemsAsync(priorFrom, priorTo, cancellationToken).ConfigureAwait(false);

        decimal curRevenue = currentItems.Sum(si => (si.UnitPrice.Amount * si.Quantity) - si.Discount.Amount);
        decimal curCost = currentItems.Sum(si => si.UnitCost.Amount * si.Quantity);
        decimal priRevenue = priorItems.Sum(si => (si.UnitPrice.Amount * si.Quantity) - si.Discount.Amount);
        decimal priCost = priorItems.Sum(si => si.UnitCost.Amount * si.Quantity);

        decimal growthPercent = priRevenue > 0
            ? (curRevenue - priRevenue) / priRevenue * 100m
            : 0m;

        return new PeriodComparisonResult(
            curRevenue, priRevenue,
            curCost, priCost,
            curRevenue - curCost, priRevenue - priCost,
            growthPercent);
    }

    public async Task<IReadOnlyList<PlatformBreakdownResult>> GetPlatformBreakdownAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var (fromOffset, toOffset) = ToRange(from, to);

        var payments = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .Where(s => s.CreatedAt >= fromOffset && s.CreatedAt <= toOffset)
            .Include(s => s.Payments)
            .SelectMany(s => s.Payments)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return payments
            .GroupBy(p =>
            {
                if (!string.IsNullOrWhiteSpace(p.BankOrPlatform))
                {
                    return p.BankOrPlatform;
                }
                return p.Method switch
                {
                    PaymentMethod.Cash => "Efectivo",
                    PaymentMethod.DebitCard => "Tarjeta Débito",
                    PaymentMethod.CreditCard => "Tarjeta Crédito",
                    PaymentMethod.Transfer => "Transferencia",
                    PaymentMethod.LoyaltyPoints => "Puntos",
                    _ => "Otro"
                };
            })
            .Select(g => new PlatformBreakdownResult(g.Key, g.Sum(p => p.Amount.Amount), g.Count()))
            .OrderByDescending(r => r.TotalAmount)
            .ToList();
    }

    private static (DateTimeOffset From, DateTimeOffset To) ToRange(DateOnly from, DateOnly to) =>
        (new DateTimeOffset(from, TimeOnly.MinValue, TimeSpan.Zero), new DateTimeOffset(to, TimeOnly.MaxValue, TimeSpan.Zero));

    // EF Core's SQLite provider cannot translate Sum/GroupBy over Money.Amount (a HasConversion-mapped
    // value object, not an owned type) into SQL — it throws "could not be translated" regardless of the
    // date range. The date/status filter still runs server-side; only the aggregation happens in-memory,
    // same pattern as ReportService.GenerateDailySalesReportAsync.
    private async Task<List<POS.Domain.Entities.SaleItem>> CompletedSaleItemsAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var (fromOffset, toOffset) = ToRange(from, to);

        var sales = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .Where(s => s.CreatedAt >= fromOffset && s.CreatedAt <= toOffset)
            .Include(s => s.Items)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return sales.SelectMany(s => s.Items).ToList();
    }
}
