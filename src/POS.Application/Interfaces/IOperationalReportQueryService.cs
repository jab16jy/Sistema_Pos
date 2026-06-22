using POS.Application.DTOs.Reporting;

namespace POS.Application.Interfaces;

/// <summary>
/// Read-only port for fetching operational totals from persisted records.
/// Implemented in Infrastructure using real AppDbContext with AsNoTracking queries.
/// </summary>
public interface IOperationalReportQueryService
{
    /// <summary>
    /// Returns operational totals — billed sales, cash, digital, and receivable settlements —
    /// for all activity that falls within the requested date range.
    /// </summary>
    Task<OperationalReportDto> GetTotalsAsync(ReportRangeRequest request, CancellationToken cancellationToken = default);
}
