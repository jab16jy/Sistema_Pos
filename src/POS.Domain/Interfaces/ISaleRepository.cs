using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface ISaleRepository
{
    Task AddAsync(Sale sale, CancellationToken cancellationToken = default);
    Task<DashboardSummaryData> GetDashboardSummaryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}

public sealed record DashboardSummaryData(
    decimal SalesTotal,
    decimal CashInDrawer,
    decimal DigitalPayments,
    int LowStockCount,
    int SalesCount);
