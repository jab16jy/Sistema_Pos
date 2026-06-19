using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

public sealed class ReportService : IReportService
{
    public Task<byte[]> GenerateDailySalesReportAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        // PDF/Excel generation belongs here. Do not put report library calls in ViewModels.
        throw new NotImplementedException("Daily sales report generation is not implemented yet.");
    }
}
