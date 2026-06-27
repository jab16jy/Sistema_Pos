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

    /// <summary>
    /// Loads sale items with their parent sale's CreatedAt for in-memory date filtering.
    /// SQLite cannot translate DateTimeOffset comparisons, so we project raw columns
    /// and filter/aggregate in memory.
    /// </summary>
    private async Task<List<SaleItemRow>> LoadSaleItemsAsync(CancellationToken ct)
    {
        return await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .SelectMany(s => s.Items.Select(si => new SaleItemRow
            {
                CreatedAt = s.CreatedAt,
                ProductId = si.ProductId,
                ProductName = si.ProductName,
                UnitPriceAmount = si.UnitPrice.Amount,
                UnitCostAmount = si.UnitCost.Amount,
                Quantity = si.Quantity,
                DiscountAmount = si.Discount.Amount
            }))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    private async Task<List<PaymentRow>> LoadPaymentsAsync(CancellationToken ct)
    {
        return await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .SelectMany(s => s.Payments.Select(p => new PaymentRow
            {
                CreatedAt = s.CreatedAt,
                BankOrPlatform = p.BankOrPlatform,
                AmountValue = p.Amount.Amount
            }))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProductMarginResult>> GetMarginByProductAsync(
        DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var fromOffset = new DateTimeOffset(from, TimeOnly.MinValue, TimeSpan.Zero);
        var toOffset = new DateTimeOffset(to, TimeOnly.MaxValue, TimeSpan.Zero);

        var allItems = await LoadSaleItemsAsync(cancellationToken).ConfigureAwait(false);

        var results = allItems
            .Where(x => x.CreatedAt >= fromOffset && x.CreatedAt <= toOffset)
            .GroupBy(x => new { x.ProductId, x.ProductName })
            .Select(g =>
            {
                var totalRevenue = g.Sum(x => (x.UnitPriceAmount * x.Quantity) - x.DiscountAmount);
                var totalCost = g.Sum(x => x.UnitCostAmount * x.Quantity);
                var margin = totalRevenue - totalCost;
                var marginPercent = totalRevenue > 0 ? margin / totalRevenue * 100m : 0m;
                return new ProductMarginResult(
                    g.Key.ProductId,
                    g.Key.ProductName,
                    totalRevenue,
                    totalCost,
                    margin,
                    marginPercent);
            })
            .OrderByDescending(r => r.GrossMargin)
            .ToList();

        return results;
    }

    public async Task<IReadOnlyList<TopSellerResult>> GetTopSellersAsync(
        DateOnly from, DateOnly to, int top = 10, CancellationToken cancellationToken = default)
    {
        var fromOffset = new DateTimeOffset(from, TimeOnly.MinValue, TimeSpan.Zero);
        var toOffset = new DateTimeOffset(to, TimeOnly.MaxValue, TimeSpan.Zero);

        var allItems = await LoadSaleItemsAsync(cancellationToken).ConfigureAwait(false);

        var results = allItems
            .Where(x => x.CreatedAt >= fromOffset && x.CreatedAt <= toOffset)
            .GroupBy(x => new { x.ProductId, x.ProductName })
            .Select(g => new TopSellerResult(
                g.Key.ProductId,
                g.Key.ProductName,
                (int)g.Sum(x => x.Quantity),
                g.Sum(x => (x.UnitPriceAmount * x.Quantity) - x.DiscountAmount)))
            .OrderByDescending(r => r.UnitsSold)
            .Take(top)
            .ToList();

        return results;
    }

    public async Task<PeriodComparisonResult> GetPeriodComparisonAsync(
        DateOnly currentFrom, DateOnly currentTo,
        DateOnly priorFrom, DateOnly priorTo,
        CancellationToken cancellationToken = default)
    {
        var curFromOffset = new DateTimeOffset(currentFrom, TimeOnly.MinValue, TimeSpan.Zero);
        var curToOffset = new DateTimeOffset(currentTo, TimeOnly.MaxValue, TimeSpan.Zero);
        var priFromOffset = new DateTimeOffset(priorFrom, TimeOnly.MinValue, TimeSpan.Zero);
        var priToOffset = new DateTimeOffset(priorTo, TimeOnly.MaxValue, TimeSpan.Zero);

        var allItems = await LoadSaleItemsAsync(cancellationToken).ConfigureAwait(false);

        var currentItems = allItems.Where(x => x.CreatedAt >= curFromOffset && x.CreatedAt <= curToOffset);
        var priorItems = allItems.Where(x => x.CreatedAt >= priFromOffset && x.CreatedAt <= priToOffset);

        var curRevenue = currentItems.Sum(x => (x.UnitPriceAmount * x.Quantity) - x.DiscountAmount);
        var curCost = currentItems.Sum(x => x.UnitCostAmount * x.Quantity);
        var priRevenue = priorItems.Sum(x => (x.UnitPriceAmount * x.Quantity) - x.DiscountAmount);
        var priCost = priorItems.Sum(x => x.UnitCostAmount * x.Quantity);

        var growthPercent = priRevenue > 0
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
        var fromOffset = new DateTimeOffset(from, TimeOnly.MinValue, TimeSpan.Zero);
        var toOffset = new DateTimeOffset(to, TimeOnly.MaxValue, TimeSpan.Zero);

        var payments = await LoadPaymentsAsync(cancellationToken).ConfigureAwait(false);

        var results = payments
            .Where(x => x.CreatedAt >= fromOffset && x.CreatedAt <= toOffset)
            .GroupBy(x => x.BankOrPlatform ?? "Unknown")
            .Select(g => new PlatformBreakdownResult(
                g.Key,
                g.Sum(x => x.AmountValue),
                g.Count()))
            .OrderByDescending(r => r.TotalAmount)
            .ToList();

        return results;
    }

    // Internal projection types for clean in-memory aggregation
    private sealed class SaleItemRow
    {
        public DateTimeOffset CreatedAt { get; init; }
        public Guid ProductId { get; init; }
        public string ProductName { get; init; } = string.Empty;
        public decimal UnitPriceAmount { get; init; }
        public decimal UnitCostAmount { get; init; }
        public decimal Quantity { get; init; }
        public decimal DiscountAmount { get; init; }
    }

    private sealed class PaymentRow
    {
        public DateTimeOffset CreatedAt { get; init; }
        public string? BankOrPlatform { get; init; }
        public decimal AmountValue { get; init; }
    }
}
