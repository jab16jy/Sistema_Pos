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
        var fromOffset = new DateTimeOffset(from, TimeOnly.MinValue, TimeSpan.Zero);
        var toOffset = new DateTimeOffset(to, TimeOnly.MaxValue, TimeSpan.Zero);

        var results = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .Where(s => s.CreatedAt >= fromOffset && s.CreatedAt <= toOffset)
            .SelectMany(s => s.Items)
            .GroupBy(si => new { si.ProductId, si.ProductName })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.ProductName,
                TotalRevenue = g.Sum(si => (si.UnitPrice.Amount * si.Quantity) - si.Discount.Amount),
                TotalCost = g.Sum(si => si.UnitCost.Amount * si.Quantity)
            })
            .OrderByDescending(x => x.TotalRevenue - x.TotalCost)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return results
            .Select(x =>
            {
                var margin = x.TotalRevenue - x.TotalCost;
                var marginPercent = x.TotalRevenue > 0 ? margin / x.TotalRevenue * 100m : 0m;
                return new ProductMarginResult(
                    x.ProductId,
                    x.ProductName,
                    x.TotalRevenue,
                    x.TotalCost,
                    margin,
                    marginPercent);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<TopSellerResult>> GetTopSellersAsync(
        DateOnly from, DateOnly to, int top = 10, CancellationToken cancellationToken = default)
    {
        var fromOffset = new DateTimeOffset(from, TimeOnly.MinValue, TimeSpan.Zero);
        var toOffset = new DateTimeOffset(to, TimeOnly.MaxValue, TimeSpan.Zero);

        return await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .Where(s => s.CreatedAt >= fromOffset && s.CreatedAt <= toOffset)
            .SelectMany(s => s.Items)
            .GroupBy(si => new { si.ProductId, si.ProductName })
            .Select(g => new TopSellerResult(
                g.Key.ProductId,
                g.Key.ProductName,
                (int)g.Sum(si => si.Quantity),
                g.Sum(si => (si.UnitPrice.Amount * si.Quantity) - si.Discount.Amount)))
            .OrderByDescending(r => r.UnitsSold)
            .Take(top)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
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

        // Server-side aggregation: SumAsync pushes aggregation to SQLite
        var curRevenue = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .Where(s => s.CreatedAt >= curFromOffset && s.CreatedAt <= curToOffset)
            .SelectMany(s => s.Items)
            .SumAsync(si => (si.UnitPrice.Amount * si.Quantity) - si.Discount.Amount, cancellationToken)
            .ConfigureAwait(false);

        var curCost = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .Where(s => s.CreatedAt >= curFromOffset && s.CreatedAt <= curToOffset)
            .SelectMany(s => s.Items)
            .SumAsync(si => si.UnitCost.Amount * si.Quantity, cancellationToken)
            .ConfigureAwait(false);

        var priRevenue = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .Where(s => s.CreatedAt >= priFromOffset && s.CreatedAt <= priToOffset)
            .SelectMany(s => s.Items)
            .SumAsync(si => (si.UnitPrice.Amount * si.Quantity) - si.Discount.Amount, cancellationToken)
            .ConfigureAwait(false);

        var priCost = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .Where(s => s.CreatedAt >= priFromOffset && s.CreatedAt <= priToOffset)
            .SelectMany(s => s.Items)
            .SumAsync(si => si.UnitCost.Amount * si.Quantity, cancellationToken)
            .ConfigureAwait(false);

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

        return await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .Where(s => s.CreatedAt >= fromOffset && s.CreatedAt <= toOffset)
            .SelectMany(s => s.Payments)
            .GroupBy(p => p.BankOrPlatform ?? "Unknown")
            .Select(g => new PlatformBreakdownResult(
                g.Key,
                g.Sum(p => p.Amount.Amount),
                g.Count()))
            .OrderByDescending(r => r.TotalAmount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
