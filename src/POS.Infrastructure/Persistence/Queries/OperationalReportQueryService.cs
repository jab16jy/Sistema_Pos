using System;
using System.Collections.Generic;
using System.Linq;
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
            .Where(s => s.Status == SaleStatus.Completed || s.Status == SaleStatus.Returned)
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
        var settlementRows = await _dbContext.ReceivableSettlements
            .AsNoTracking()
            .Select(rs => new { rs.Amount, rs.Method, rs.AppliedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var filteredSettlements = settlementRows
            .Where(rs => rs.AppliedAt >= request.From && rs.AppliedAt <= request.To)
            .ToList();

        decimal receivableSettlementTotal = filteredSettlements.Sum(rs => rs.Amount.Amount);
        decimal receivableSettlementCash = filteredSettlements
            .Where(rs => rs.Method == PaymentMethod.Cash)
            .Sum(rs => rs.Amount.Amount);
        decimal receivableSettlementDigital = filteredSettlements
            .Where(rs => rs.Method != PaymentMethod.Cash)
            .Sum(rs => rs.Amount.Amount);
        var returnSettlementRows = await _dbContext.ReturnSettlements
            .AsNoTracking()
            .Select(rs => new { rs.Amount, rs.PaymentMethod, rs.SettledAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var filteredReturnSettlements = returnSettlementRows
            .Where(rs => rs.SettledAt >= request.From && rs.SettledAt <= request.To)
            .ToList();

        decimal cashReturnOutflows = filteredReturnSettlements
            .Where(rs => rs.PaymentMethod == PaymentMethod.Cash)
            .Sum(rs => rs.Amount.Amount);
        decimal digitalReturnOutflows = filteredReturnSettlements
            .Where(rs => rs.PaymentMethod is PaymentMethod.DebitCard or PaymentMethod.CreditCard or PaymentMethod.Transfer)
            .Sum(rs => rs.Amount.Amount);

        decimal expectedDrawerCash = saleCashCollected + receivableSettlementCash - cashReturnOutflows;

        // Generate chart points based on the date range
        var chartPoints = new List<OperationalReportChartPointDto>();
        double totalDays = (request.To - request.From).TotalDays;

        if (totalDays <= 1.5)
        {
            // Group by hour
            var start = request.From;
            var end = request.To;
            var current = new DateTimeOffset(start.Year, start.Month, start.Day, start.Hour, 0, 0, start.Offset);
            while (current <= end)
            {
                var next = current.AddHours(1);
                var salesInBucket = filteredSales.Where(s => s.CreatedAt >= current && s.CreatedAt < next).ToList();
                decimal amount = salesInBucket.Sum(s => s.Items.Sum(si => (si.UnitPrice.Amount * si.Quantity) - si.Discount.Amount));
                string label = current.ToString("HH:00");
                string toolTip = $"{label}: ${amount:N0} ({salesInBucket.Count} trans.)";
                chartPoints.Add(new OperationalReportChartPointDto(label, amount, toolTip));
                current = next;
            }
        }
        else if (totalDays <= 31)
        {
            // Group by day
            var start = request.From.Date;
            var end = request.To.Date;
            var current = new DateTimeOffset(start, TimeSpan.Zero);
            var limit = new DateTimeOffset(end, TimeSpan.Zero).AddDays(1);
            while (current < limit)
            {
                var next = current.AddDays(1);
                var salesInBucket = filteredSales.Where(s => s.CreatedAt >= current && s.CreatedAt < next).ToList();
                decimal amount = salesInBucket.Sum(s => s.Items.Sum(si => (si.UnitPrice.Amount * si.Quantity) - si.Discount.Amount));
                string label = current.ToString("dd/MM");
                string toolTip = $"{current:dd/MM/yyyy}: ${amount:N0} ({salesInBucket.Count} trans.)";
                chartPoints.Add(new OperationalReportChartPointDto(label, amount, toolTip));
                current = next;
            }
        }
        else if (totalDays <= 90)
        {
            // Group by week
            var current = request.From;
            int weekNum = 1;
            while (current <= request.To)
            {
                var next = current.AddDays(7);
                var salesInBucket = filteredSales.Where(s => s.CreatedAt >= current && s.CreatedAt < next).ToList();
                decimal amount = salesInBucket.Sum(s => s.Items.Sum(si => (si.UnitPrice.Amount * si.Quantity) - si.Discount.Amount));
                string label = $"Sem {weekNum}";
                string toolTip = $"{current:dd/MM} - {next.AddDays(-1):dd/MM}: ${amount:N0} ({salesInBucket.Count} trans.)";
                chartPoints.Add(new OperationalReportChartPointDto(label, amount, toolTip));
                current = next;
                weekNum++;
            }
        }
        else
        {
            // Group by month
            var start = new DateTimeOffset(request.From.Year, request.From.Month, 1, 0, 0, 0, request.From.Offset);
            var current = start;
            while (current <= request.To)
            {
                var next = current.AddMonths(1);
                var salesInBucket = filteredSales.Where(s => s.CreatedAt >= current && s.CreatedAt < next).ToList();
                decimal amount = salesInBucket.Sum(s => s.Items.Sum(si => (si.UnitPrice.Amount * si.Quantity) - si.Discount.Amount));
                string label = current.ToString("MMM-yy");
                string toolTip = $"{current:MMMM yyyy}: ${amount:N0} ({salesInBucket.Count} trans.)";
                chartPoints.Add(new OperationalReportChartPointDto(label, amount, toolTip));
                current = next;
            }
        }

        return new OperationalReportDto(
            BilledSalesTotal: billedSalesTotal,
            SaleCashCollected: saleCashCollected,
            SaleDigitalCollected: saleDigitalCollected,
            ReceivableSettlementCash: receivableSettlementCash,
            ReceivableSettlementDigital: receivableSettlementDigital,
            ReceivableSettlementTotal: receivableSettlementTotal,
            ExpectedDrawerCash: expectedDrawerCash,
            ChartPoints: chartPoints,
            CashReturnOutflows: cashReturnOutflows,
            DigitalReturnOutflows: digitalReturnOutflows);
    }
}
