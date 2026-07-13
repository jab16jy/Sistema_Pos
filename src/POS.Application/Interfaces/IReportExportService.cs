using POS.Application.DTOs.Reporting;
using POS.Application.DTOs.Shifts;

namespace POS.Application.Interfaces;

public interface IReportExportService
{
    Task<byte[]> ExportOperationalReportPdfAsync(OperationalReportDto report, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<byte[]> ExportOperationalReportExcelAsync(OperationalReportDto report, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
    Task<byte[]> ExportProfitReportPdfAsync(IReadOnlyList<TopSellerResult> topSellers, PeriodComparisonResult comparison, IReadOnlyList<ProductMarginResult> margins, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<byte[]> ExportProfitReportExcelAsync(IReadOnlyList<TopSellerResult> topSellers, PeriodComparisonResult comparison, IReadOnlyList<ProductMarginResult> margins, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<byte[]> ExportClosingsReportPdfAsync(IReadOnlyList<ShiftSummary> shifts, CancellationToken ct = default);
    Task<byte[]> ExportClosingsReportExcelAsync(IReadOnlyList<ShiftSummary> shifts, CancellationToken ct = default);
}
