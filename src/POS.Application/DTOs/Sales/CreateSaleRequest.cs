using POS.Domain.Entities;
using POS.Domain.Enums;

namespace POS.Application.DTOs.Sales;

/// <summary>
/// Request to create a confirmed sale.
/// Use the SettlementPlan overload when a deferred balance is involved;
/// use the Payments overload for the legacy full-payment path (backward compatible).
/// </summary>
public sealed record CreateSaleRequest
{
    /// <summary>Full-payment (legacy) constructor. Preserved for backward compatibility.</summary>
    public CreateSaleRequest(
        Guid cashierUserId,
        Guid? customerId,
        IReadOnlyCollection<CreateSaleItemRequest> items,
        IReadOnlyCollection<CreateSalePaymentRequest> payments,
        int redeemLoyaltyPoints = 0)
    {
        CashierUserId = cashierUserId;
        CustomerId = customerId;
        Items = items;
        Payments = payments;
        SettlementPlan = null;
        RedeemLoyaltyPoints = redeemLoyaltyPoints;
    }

    /// <summary>Settlement-plan constructor. Use this when deferred balance may be > 0.</summary>
    public CreateSaleRequest(
        Guid cashierUserId,
        Guid? customerId,
        IReadOnlyCollection<CreateSaleItemRequest> items,
        SaleSettlementPlan settlementPlan)
    {
        CashierUserId = cashierUserId;
        CustomerId = customerId;
        Items = items;
        Payments = null;
        SettlementPlan = settlementPlan;
    }

    public Guid CashierUserId { get; }
    public Guid? CustomerId { get; }
    public IReadOnlyCollection<CreateSaleItemRequest> Items { get; }

    /// <summary>Legacy explicit payment lines. Null when SettlementPlan is used.</summary>
    public IReadOnlyCollection<CreateSalePaymentRequest>? Payments { get; }

    /// <summary>Settlement plan describing collected + deferred amounts. Null when Payments is used.</summary>
    public SaleSettlementPlan? SettlementPlan { get; }

    /// <summary>
    /// Loyalty points the customer redeems on this sale. Zero means no redemption.
    /// The redemption is validated, applied as a LoyaltyPoints tender, and recorded
    /// atomically with the sale by CreateSaleUseCase.
    /// </summary>
    public int RedeemLoyaltyPoints { get; init; }
}

public sealed record CreateSaleItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal Discount);

public sealed record CreateSalePaymentRequest(
    PaymentMethod Method,
    decimal Amount,
    decimal? ReceivedAmount,
    string? BankOrPlatform);
