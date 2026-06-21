using POS.Application.DTOs.Sales;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Sales;

public sealed class GetDashboardSummaryUseCase
{
    private readonly ISaleRepository _sales;

    public GetDashboardSummaryUseCase(ISaleRepository sales)
    {
        _sales = sales;
    }

    public async Task<DashboardSummary> ExecuteAsync(
        DashboardSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        DashboardSummaryData summary = await _sales.GetDashboardSummaryAsync(
            request.From,
            request.To,
            cancellationToken);

        return new DashboardSummary(
            summary.SalesTotal,
            summary.CashInDrawer,
            summary.DigitalPayments,
            summary.LowStockCount,
            summary.SalesCount);
    }
}
