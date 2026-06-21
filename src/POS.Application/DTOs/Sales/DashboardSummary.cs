namespace POS.Application.DTOs.Sales;

public sealed record DashboardSummary(
    decimal SalesTotal,
    decimal CashInDrawer,
    decimal DigitalPayments,
    int LowStockCount,
    int SalesCount);

public sealed record DashboardSummaryRequest(DateTimeOffset From, DateTimeOffset To);
