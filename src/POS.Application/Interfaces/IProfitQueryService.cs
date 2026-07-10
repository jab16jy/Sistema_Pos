using POS.Application.DTOs.Reporting;

namespace POS.Application.Interfaces;

/// <summary>
/// Port for profit and analytics queries. The date/status filter runs server-side; aggregation happens
/// in-memory over the materialized rows, because EF Core's SQLite provider cannot translate Sum/GroupBy
/// over Money.Amount (a value-converted type, not an owned type).
/// </summary>
public interface IProfitQueryService
{
    Task<IReadOnlyList<ProductMarginResult>> GetMarginByProductAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TopSellerResult>> GetTopSellersAsync(DateOnly from, DateOnly to, int top = 10, CancellationToken cancellationToken = default);
    Task<PeriodComparisonResult> GetPeriodComparisonAsync(DateOnly currentFrom, DateOnly currentTo, DateOnly priorFrom, DateOnly priorTo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformBreakdownResult>> GetPlatformBreakdownAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}
