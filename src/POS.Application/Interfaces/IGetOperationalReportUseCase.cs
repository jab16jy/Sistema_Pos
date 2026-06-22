using POS.Application.DTOs.Reporting;

namespace POS.Application.Interfaces;

/// <summary>
/// Application port for generating an operational report.
/// ReportsViewModel depends on this interface, not the concrete use case.
/// </summary>
public interface IGetOperationalReportUseCase
{
    Task<OperationalReportResult> ExecuteAsync(ReportRangeRequest request, CancellationToken cancellationToken = default);
}
