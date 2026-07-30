using POS.Domain.Enums;

namespace POS.Application.DTOs.Loyalty;

/// <summary>Current loyalty balance and account status for a customer.</summary>
public sealed record LoyaltyBalanceResult(
    Guid CustomerId,
    Guid LoyaltyAccountId,
    int Balance,
    bool IsActive,
    IReadOnlyList<LoyaltyTransactionResult> RecentTransactions,
    decimal RedemptionValuePerPoint = 0m,
    int MinimumRedeemPoints = 0,
    LoyaltyTierResult? CurrentTier = null);

/// <summary>Summary of a single loyalty point event.</summary>
public sealed record LoyaltyTransactionResult(
    Guid Id,
    int Points,
    string Type,
    Guid? SaleId,
    string Description,
    DateTimeOffset CreatedAt,
    string? CustomerName = null,
    string? InvoiceNumber = null,
    string? ResponsibleUser = null);

/// <summary>Business ceilings a tier's automatic benefits must respect.</summary>
public sealed record LoyaltyTierLimitsResult(
    decimal MaximumDiscountPercent,
    decimal MaximumPointsMultiplier);

/// <summary>Editable membership tier: calculated benefits plus the free-text benefits the business defines.</summary>
public sealed record LoyaltyTierResult(
    Guid Id,
    string Name,
    decimal MinPoints,
    decimal DiscountPercent,
    decimal PointsMultiplier,
    string Color,
    LoyaltyTierTextForeground TextForeground,
    string Benefits = "",
    bool AllowsRedemption = false);

public sealed record CreateLoyaltyTierRequest(
    string Name,
    decimal MinPoints,
    decimal DiscountPercent,
    decimal PointsMultiplier,
    string Color = "#64748B",
    LoyaltyTierTextForeground TextForeground = LoyaltyTierTextForeground.White,
    string? Benefits = null,
    bool AllowsRedemption = false);

public sealed record UpdateLoyaltyTierRequest(
    Guid Id,
    string Name,
    decimal MinPoints,
    decimal DiscountPercent,
    decimal PointsMultiplier,
    string Color = "#64748B",
    LoyaltyTierTextForeground TextForeground = LoyaltyTierTextForeground.White,
    string? Benefits = null,
    bool AllowsRedemption = false);

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

/// <summary>A reward from the catalogue, priced in points.</summary>
public sealed record LoyaltyBenefitResult(
    Guid Id,
    string Name,
    string Description,
    int PointsCost,
    bool IsActive);

/// <summary>Creates a benefit when <see cref="BenefitId"/> is null, updates it otherwise.</summary>
public sealed record SaveLoyaltyBenefitRequest(
    string Name,
    int PointsCost,
    string? Description = null,
    bool IsActive = true,
    Guid? BenefitId = null);

/// <summary>Request to spend a customer's points on a catalogue benefit.</summary>
public sealed record ClaimLoyaltyBenefitRequest(
    Guid CustomerId,
    Guid BenefitId,
    Guid? ClaimedByUserId = null);

/// <summary>A customer's benefit claim and its delivery state.</summary>
public sealed record LoyaltyBenefitClaimResult(
    Guid Id,
    Guid BenefitId,
    string BenefitName,
    int PointsSpent,
    LoyaltyBenefitClaimStatus Status,
    DateTimeOffset ClaimedAt,
    DateTimeOffset? DeliveredAt = null,
    DateTimeOffset? CancelledAt = null,
    string? CancellationReason = null);

/// <summary>Real program-wide loyalty metrics, derived from persisted accounts and transactions.</summary>
public sealed record LoyaltyProgramOverviewResult(
    LoyaltyRuleResult? ActiveRule,
    int TotalPointsIssued,
    int EnrolledCustomers,
    int RedemptionsThisMonth,
    IReadOnlyList<LoyaltyTransactionResult> RecentTransactions);
