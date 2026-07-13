namespace POS.Application.DTOs.Reporting;

public sealed record ProductMarginResult(
    Guid ProductId,
    string ProductName,
    decimal TotalRevenue,
    decimal TotalCost,
    decimal GrossMargin,
    decimal MarginPercent);

public sealed record TopSellerResult(
    Guid ProductId,
    string ProductName,
    int UnitsSold,
    decimal TotalRevenue,
    int TransactionCount = 0,
    int UniqueCustomersCount = 0);

public sealed record PeriodComparisonResult(
    decimal CurrentRevenue,
    decimal PriorRevenue,
    decimal CurrentCost,
    decimal PriorCost,
    decimal CurrentMargin,
    decimal PriorMargin,
    decimal RevenueGrowthPercent);

public sealed record PlatformBreakdownResult(
    string Platform,
    decimal TotalAmount,
    int TransactionCount);
