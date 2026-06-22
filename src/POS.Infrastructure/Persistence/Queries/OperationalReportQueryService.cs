using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Reporting;
using POS.Application.Interfaces;
using POS.Domain.Enums;
using POS.Infrastructure.Persistence;

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
        var salesInRange = await _dbContext.Sales
            .AsNoTracking()
            .Include(s => s.Items)
            .Include(s => s.Payments)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var relevantSales = salesInRange
            .Where(s => s.CreatedAt >= request.From && s.CreatedAt <= request.To &&
                        s.Status == SaleStatus.Completed)
            .ToList();

        decimal billedSalesTotal = relevantSales.Sum(s => s.Total().Amount);
        decimal saleCashCollected = relevantSales.Sum(s => s.CashInDrawer().Amount);
        decimal saleDigitalCollected = relevantSales
            .SelectMany(s => s.Payments)
            .Where(p => p.Method != PaymentMethod.Cash)
            .Sum(p => p.Amount.Amount);

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
