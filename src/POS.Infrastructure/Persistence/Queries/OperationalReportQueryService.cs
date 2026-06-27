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
        // SQLite cannot translate DateTimeOffset comparisons in LINQ, so we load
        // completed sales with their items/payments and filter by date in memory.
        // Aggregation is then performed on the filtered in-memory set.
        var completedSales = await _dbContext.Sales
            .AsNoTracking()
            .Where(s => s.Status == SaleStatus.Completed)
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var relevantSales = completedSales
            .Where(s => s.CreatedAt >= request.From && s.CreatedAt <= request.To)
            .ToList();

        decimal billedSalesTotal = relevantSales
            .SelectMany(s => s.Items)
            .Sum(si => (si.UnitPrice.Amount * si.Quantity) - si.Discount.Amount);

        decimal saleCashCollected = relevantSales
            .SelectMany(s => s.Payments)
            .Where(p => p.Method == PaymentMethod.Cash)
            .Sum(p => p.Amount.Amount);

        decimal saleDigitalCollected = relevantSales
            .SelectMany(s => s.Payments)
            .Where(p => p.Method != PaymentMethod.Cash)
            .Sum(p => p.Amount.Amount);

        // Receivable settlements — also loaded and filtered in memory for SQLite compat
        var allSettlements = await _dbContext.ReceivableSettlements
            .AsNoTracking()
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
