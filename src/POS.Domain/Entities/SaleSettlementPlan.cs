using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>One leg collected immediately as part of a settlement plan (any payment method).</summary>
public sealed record SettlementCollectedPayment(
    PaymentMethod Method,
    decimal Amount,
    string? BankOrPlatform = null);

/// <summary>
/// Zero or more legs collected immediately (any mix of methods) plus the balance deferred as a
/// receivable. Invariant: sum(CollectedPayments.Amount) + DeferredBalanceAmount must equal the sale total.
/// </summary>
public sealed record SaleSettlementPlan(
    IReadOnlyList<SettlementCollectedPayment> CollectedPayments,
    decimal DeferredBalanceAmount)
{
    /// <summary>
    /// Backward-compatible convenience constructor wrapping the old single-leg shape into a
    /// 0-or-1-element list, so pre-existing single-tender call sites keep compiling unchanged.
    /// </summary>
    public SaleSettlementPlan(decimal collectedAmount, PaymentMethod? collectedMethod, decimal deferredBalanceAmount, string? collectedBankOrPlatform = null)
        : this(
            collectedMethod is { } m ? [new SettlementCollectedPayment(m, collectedAmount, collectedBankOrPlatform)] : [],
            deferredBalanceAmount)
    {
    }
}
