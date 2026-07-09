using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Reporting;
using POS.Application.Interfaces;
using POS.Domain.Enums;

namespace POS.Infrastructure.Persistence.Queries;

public sealed class OperationalReportQueryService : IOperationalReportQueryService
{
    private readonly AppDbContext _dbContext;

    public OperationalReportQueryService(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<OperationalReportDto> GetTotalsAsync(
        ReportRangeRequest request,
        CancellationToken cancellationToken = default)
    {
        // SQLite limitation: DateTimeOffset comparisons with SelectMany+SumAsync cannot be translated.
        // Filter by status server-side, then evaluate date range in memory.
        // For production SQL Server/PostgreSQL, this would be fully server-side.

        var completedSales = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .Select(s => new
            {
                s.Items,
                s.Payments,
                s.CreatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var filteredSales = completedSales
            .Where(s => s.CreatedAt >= request.From && s.CreatedAt <= request.To)
            .ToList();

        // Billed sales total: sum of item subtotals for completed sales in range
        decimal billedSalesTotal = filteredSales
            .SelectMany(s => s.Items)
            .Sum(si => (si.UnitPrice.Amount * si.Quantity) - si.Discount.Amount);

        // Cash collected from sales
        decimal saleCashCollected = filteredSales
            .SelectMany(s => s.Payments)
            .Where(p => p.Method == PaymentMethod.Cash)
            .Sum(p => p.Amount.Amount);

        // Digital collected from sales.
        // LoyaltyPoints is a non-money tender (points redeemed), so it is excluded from
        // both cash and digital collections — it never enters the drawer or the bank.
        decimal saleDigitalCollected = filteredSales
            .SelectMany(s => s.Payments)
            .Where(p => p.Method != PaymentMethod.Cash && p.Method != PaymentMethod.LoyaltyPoints)
            .Sum(p => p.Amount.Amount);

        // Receivable settlements in range — SQLite limitation: DateTimeOffset comparison
        // with SumAsync cannot be translated. Materialize then filter in memory.
        var allSettlements = await _dbContext.ReceivableSettlements
            .AsNoTracking()
            .Select(rs => new { rs.Amount, rs.AppliedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        decimal receivableSettlementTotal = allSettlements
            .Where(rs => rs.AppliedAt >= request.From && rs.AppliedAt <= request.To)
            .Sum(rs => rs.Amount.Amount);

        decimal receivableSettlementCash = receivableSettlementTotal;
        decimal receivableSettlementDigital = 0m;
        decimal expectedDrawerCash = saleCashCollected + receivableSettlementCash;

        return new OperationalReportDto(
            BilledSalesTotal: billedSalesTotal,
            SaleCashCollected: saleCashCollected,
            SaleDigitalCollected: saleDigitalCollected,
            ReceivableSettlementCash: receivableSettlementCash,
            ReceivableSettlementDigital: receivableSettlementDigital,
            ReceivableSettlementTotal: receivableSettlementTotal,
            ExpectedDrawerCash: expectedDrawerCash);
    }
}
