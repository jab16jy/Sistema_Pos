using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface ISaleRepository
{
    Task AddAsync(Sale sale, CancellationToken cancellationToken = default);
    Task<Sale?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Sale>> GetOnHoldByCashierAsync(
        Guid cashierUserId,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Sale>>([]);

    Task<DashboardSummaryData> GetDashboardSummaryAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<ShiftSalesSummaryData> GetShiftSalesSummaryAsync(
        Guid cashierUserId,
        DateTimeOffset openedAt,
        CancellationToken cancellationToken = default)
        => Task.FromResult(ShiftSalesSummaryData.Empty);

    Task<ShiftSalesSummaryData> GetShiftSalesSummaryAsync(
        Guid cashierUserId,
        DateTimeOffset openedAt,
        DateTimeOffset closedAt,
        CancellationToken cancellationToken = default)
        => GetShiftSalesSummaryAsync(cashierUserId, openedAt, cancellationToken);
}

public sealed record DashboardSummaryData(
    decimal SalesTotal,
    decimal CashInDrawer,
    decimal DigitalPayments,
    int LowStockCount,
    int SalesCount);

public sealed record ShiftSalesSummaryData(
    decimal SalesTotal,
    decimal CashPayments,
    decimal DigitalPayments,
    decimal ReceivableSettlementCash = 0m,
    decimal ReceivableSettlementDigital = 0m)
{
    public static ShiftSalesSummaryData Empty { get; } = new(0m, 0m, 0m);
}
