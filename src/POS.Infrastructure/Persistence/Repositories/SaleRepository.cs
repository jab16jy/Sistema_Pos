using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class SaleRepository : ISaleRepository
{
    private readonly AppDbContext _dbContext;

    public SaleRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Sale sale, CancellationToken cancellationToken = default)
    {
        await _dbContext.Sales.AddAsync(sale, cancellationToken);
    }

    public Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Sales
            .Include(sale => sale.Items)
            .Include(sale => sale.Payments)
            .FirstOrDefaultAsync(sale => sale.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Sale>> GetOnHoldByCashierAsync(
        Guid cashierUserId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sales
            .AsNoTracking()
            .Include(sale => sale.Items)
            .Where(sale => sale.CashierUserId == cashierUserId && sale.Status == SaleStatus.OnHold)
            .OrderByDescending(sale => sale.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DashboardSummaryData> GetDashboardSummaryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset utcFrom = from.ToUniversalTime();
        DateTimeOffset utcTo = to.ToUniversalTime();

        (decimal salesTotal, decimal cashInDrawer, decimal digitalPayments, int salesCount, _, _) =
            await ReadSalesSummaryAsync(utcFrom, utcTo, cashierUserId: null, cancellationToken).ConfigureAwait(false);

        int lowStockCount = await _dbContext.Products
            .CountAsync(product => product.IsActive && product.MinimumStock > 0m && product.CurrentStock <= product.MinimumStock, cancellationToken);

        return new DashboardSummaryData(
            salesTotal,
            cashInDrawer,
            digitalPayments,
            lowStockCount,
            salesCount);
    }

    public async Task<ShiftSalesSummaryData> GetShiftSalesSummaryAsync(
        Guid cashierUserId,
        DateTimeOffset openedAt,
        CancellationToken cancellationToken = default)
        => await GetShiftSalesSummaryAsync(cashierUserId, openedAt, DateTimeOffset.UtcNow, cancellationToken)
            .ConfigureAwait(false);

    public async Task<ShiftSalesSummaryData> GetShiftSalesSummaryAsync(
        Guid cashierUserId,
        DateTimeOffset openedAt,
        DateTimeOffset closedAt,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset utcOpenedAt = openedAt.ToUniversalTime();
        DateTimeOffset utcClosedAt = closedAt.ToUniversalTime();

        (decimal salesTotal, decimal cashPayments, decimal digitalPayments, _, decimal settlementCash, decimal settlementDigital) =
            await ReadSalesSummaryAsync(utcOpenedAt, utcClosedAt, cashierUserId, cancellationToken).ConfigureAwait(false);

        return new ShiftSalesSummaryData(salesTotal, cashPayments, digitalPayments, settlementCash, settlementDigital);
    }

    private async Task<(decimal SalesTotal, decimal CashPayments, decimal DigitalPayments, int SalesCount, decimal SettlementCash, decimal SettlementDigital)> ReadSalesSummaryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? cashierUserId,
        CancellationToken cancellationToken)
    {
        DbConnection connection = _dbContext.Database.GetDbConnection();
        bool closeConnection = connection.State != System.Data.ConnectionState.Open;
        if (closeConnection)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    COALESCE((SELECT SUM(ROUND(CAST(si.unit_price AS NUMERIC) * si."Quantity" - CAST(si.discount AS NUMERIC), 2))
                              FROM SALE_ITEMS si
                              INNER JOIN SALES s ON s."Id" = si."SaleId"
                              WHERE s."Status" = 3 AND s."CreatedAt" >= $from AND s."CreatedAt" < $to
                                AND ($cashier IS NULL OR s."CashierUserId" = $cashier)), 0),
                    COALESCE((SELECT SUM(CASE WHEN p."Method" = 1 THEN CAST(p.amount AS NUMERIC) ELSE 0 END)
                              FROM PAYMENTS p
                              INNER JOIN SALES s ON s."Id" = p."SaleId"
                              WHERE s."Status" = 3 AND s."CreatedAt" >= $from AND s."CreatedAt" < $to
                                AND ($cashier IS NULL OR s."CashierUserId" = $cashier)), 0),
                    COALESCE((SELECT SUM(CASE WHEN p."Method" IN (2, 3, 4) THEN CAST(p.amount AS NUMERIC) ELSE 0 END)
                              FROM PAYMENTS p
                              INNER JOIN SALES s ON s."Id" = p."SaleId"
                              WHERE s."Status" = 3 AND s."CreatedAt" >= $from AND s."CreatedAt" < $to
                                AND ($cashier IS NULL OR s."CashierUserId" = $cashier)), 0),
                    (SELECT COUNT(*) FROM SALES s
                     WHERE s."Status" = 3 AND s."CreatedAt" >= $from AND s."CreatedAt" < $to
                       AND ($cashier IS NULL OR s."CashierUserId" = $cashier)),
                    COALESCE((SELECT SUM(CASE WHEN rs."Method" = 1 THEN CAST(rs.amount AS NUMERIC) ELSE 0 END)
                              FROM RECEIVABLE_SETTLEMENTS rs
                              INNER JOIN RECEIVABLES r ON r."Id" = rs."ReceivableId"
                              INNER JOIN SALES s ON s."Id" = r."SaleId"
                              WHERE s."Status" = 3 AND rs."AppliedAt" >= $from AND rs."AppliedAt" < $to
                                AND ($cashier IS NULL OR COALESCE(rs.cashier_user_id, s."CashierUserId") = $cashier)), 0),
                    COALESCE((SELECT SUM(CASE WHEN rs."Method" IN (2, 3, 4) THEN CAST(rs.amount AS NUMERIC) ELSE 0 END)
                              FROM RECEIVABLE_SETTLEMENTS rs
                              INNER JOIN RECEIVABLES r ON r."Id" = rs."ReceivableId"
                              INNER JOIN SALES s ON s."Id" = r."SaleId"
                              WHERE s."Status" = 3 AND rs."AppliedAt" >= $from AND rs."AppliedAt" < $to
                                AND ($cashier IS NULL OR COALESCE(rs.cashier_user_id, s."CashierUserId") = $cashier)), 0);
                """;
            command.Parameters.Add(new SqliteParameter("$from", ToSqliteTimestamp(from)));
            command.Parameters.Add(new SqliteParameter("$to", ToSqliteTimestamp(to)));
            command.Parameters.Add(new SqliteParameter(
                "$cashier",
                cashierUserId is { } id ? id.ToString() : DBNull.Value));

            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return (
                ToDecimal(reader.GetValue(0)),
                ToDecimal(reader.GetValue(1)),
                ToDecimal(reader.GetValue(2)),
                Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture),
                ToDecimal(reader.GetValue(4)),
                ToDecimal(reader.GetValue(5)));
        }
        finally
        {
            if (closeConnection)
            {
                await _dbContext.Database.CloseConnectionAsync().ConfigureAwait(false);
            }
        }
    }

    private static string ToSqliteTimestamp(DateTimeOffset value)
        => value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture);

    private static decimal ToDecimal(object value)
        => Convert.ToDecimal(value, CultureInfo.InvariantCulture);
}
