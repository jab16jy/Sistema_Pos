using POS.Application.DTOs.Reporting;
using POS.Application.Interfaces;

namespace POS.Application.UseCases.Reporting;

/// <summary>
/// Returns a separated operational report from persisted records for the given date range.
/// Validates the date range and delegates data retrieval to IOperationalReportQueryService.
/// </summary>
public sealed class GetOperationalReportUseCase : IGetOperationalReportUseCase
{
    private readonly IOperationalReportQueryService _queryService;

    public GetOperationalReportUseCase(IOperationalReportQueryService queryService)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
    }

    public async Task<OperationalReportResult> ExecuteAsync(
        ReportRangeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return new OperationalReportResult(false, null, "Report request cannot be null.");
        }

        if (request.From >= request.To)
        {
            return new OperationalReportResult(false, null,
                "Invalid date range: 'From' must be earlier than 'To'.");
        }

        var report = await _queryService.GetTotalsAsync(request, cancellationToken).ConfigureAwait(false);
        return new OperationalReportResult(true, report, null);
    }
}
