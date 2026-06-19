namespace POS.Application.Interfaces;

public interface IReportService
{
    Task<byte[]> GenerateDailySalesReportAsync(DateOnly date, CancellationToken cancellationToken = default);
}
