using POS.Domain.ValueObjects;
using POS.Domain.Enums;

namespace POS.Domain.Entities;

/// <summary>
/// Tracks an outstanding balance owed by a customer, linked to its originating sale.
/// Created inside the sale confirmation transaction whenever a deferred balance exists.
/// </summary>
public sealed class Receivable
{
    private readonly List<ReceivableSettlement> _settlements = [];

    private Receivable()
    {
    }

    public Receivable(Guid customerId, Guid saleId, Money originalBalance)
    {
        if (originalBalance.Amount <= 0m)
        {
            throw new InvalidOperationException("Receivable balance must be greater than zero.");
        }

        Id = Guid.NewGuid();
        CustomerId = customerId;
        SaleId = saleId;
        OriginalBalance = originalBalance;
        OutstandingBalance = originalBalance;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid SaleId { get; private set; }
    public Money OriginalBalance { get; private set; }
    public Money OutstandingBalance { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsCancelled { get; private set; }
    public int Version { get; private set; }
    public bool IsPaidOff => !IsCancelled && OutstandingBalance.Amount == 0m;

    public IReadOnlyCollection<ReceivableSettlement> Settlements => _settlements.AsReadOnly();

    /// <summary>
    /// Applies a partial or full payment toward the outstanding balance.
    /// Rejects overpayment per spec: payment > amount due throws.
    /// </summary>
    public void ApplyPayment(Money payment)
        => ApplyPayment(payment, PaymentMethod.Cash);

    public void ApplyPayment(Money payment, PaymentMethod method, string? bankOrPlatform = null, Guid? cashierUserId = null, Guid? shiftId = null)
    {
        if (IsCancelled)
        {
            throw new InvalidOperationException("Cancelled receivables cannot receive payments.");
        }

        if (payment.Amount <= 0m)
        {
            throw new InvalidOperationException("Payment amount must be greater than zero.");
        }

        if (method == PaymentMethod.LoyaltyPoints)
        {
            throw new InvalidOperationException("Loyalty points cannot be used to pay a receivable.");
        }

        if (payment.Amount > OutstandingBalance.Amount)
        {
            throw new InvalidOperationException("Payment amount exceeds outstanding balance.");
        }

        _settlements.Add(new ReceivableSettlement(Id, payment, method, bankOrPlatform, cashierUserId, shiftId));
        OutstandingBalance = OutstandingBalance - payment;
        Version++;
    }

    public void Cancel()
    {
        if (IsCancelled)
        {
            return;
        }

        IsCancelled = true;
        OutstandingBalance = Money.Zero;
        Version++;
    }

    public void ReduceForReturn(Money refundAmount)
    {
        if (IsCancelled || refundAmount.Amount <= 0m)
        {
            return;
        }

        OutstandingBalance = refundAmount.Amount >= OutstandingBalance.Amount
            ? Money.Zero
            : OutstandingBalance - refundAmount;
        Version++;
    }
}
