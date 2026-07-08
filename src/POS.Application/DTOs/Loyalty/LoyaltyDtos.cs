namespace POS.Application.DTOs.Loyalty;

/// <summary>Current loyalty balance and account status for a customer.</summary>
public sealed record LoyaltyBalanceResult(
    Guid CustomerId,
    Guid LoyaltyAccountId,
    int Balance,
    bool IsActive,
    IReadOnlyList<LoyaltyTransactionResult> RecentTransactions);

/// <summary>Summary of a single loyalty point event.</summary>
public sealed record LoyaltyTransactionResult(
    Guid Id,
    int Points,
    string Type,
    Guid? SaleId,
    string Description,
    DateTimeOffset CreatedAt);

/// <summary>Request to accumulate points after a sale.</summary>
public sealed record EarnPointsRequest(
    Guid CustomerId,
    Guid SaleId,
    decimal SaleTotal);

/// <summary>Request to redeem points at checkout.</summary>
public sealed record RedeemPointsRequest(
    Guid CustomerId,
    int PointsToRedeem,
    decimal? SaleTotal = null);

/// <summary>Result of a successful redemption — includes the COP discount amount.</summary>
public sealed record RedeemPointsResult(
    int PointsRedeemed,
    decimal DiscountAmount);

/// <summary>Request to configure a new loyalty rule.</summary>
public sealed record SetLoyaltyRuleRequest(
    decimal PointsPerCurrencyUnit,
    decimal RedemptionValuePerPoint,
    int MinimumRedeemPoints,
    string? Description = null);

/// <summary>Current persisted loyalty rule shown in the admin module.</summary>
public sealed record LoyaltyRuleResult(
    Guid Id,
    decimal PointsPerCurrencyUnit,
    decimal RedemptionValuePerPoint,
    int MinimumRedeemPoints,
    string Description,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>Real program-wide loyalty metrics, derived from persisted accounts and transactions.</summary>
public sealed record LoyaltyProgramOverviewResult(
    LoyaltyRuleResult? ActiveRule,
    int TotalPointsIssued,
    int EnrolledCustomers,
    int RedemptionsThisMonth,
    IReadOnlyList<LoyaltyTransactionResult> RecentTransactions);
