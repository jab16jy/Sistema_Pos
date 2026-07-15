using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Describes how a sale is to be settled: the collected amount (paid immediately),
/// the payment method for the collected portion, and the balance deferred as a receivable.
/// Invariant: CollectedAmount + DeferredBalanceAmount must equal the sale total.
/// </summary>
public sealed record SaleSettlementPlan(
    decimal CollectedAmount,
    PaymentMethod? CollectedMethod,
    decimal DeferredBalanceAmount,
    string? CollectedBankOrPlatform = null);
