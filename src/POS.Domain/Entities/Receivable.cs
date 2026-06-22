using POS.Domain.ValueObjects;

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
    public bool IsPaidOff => OutstandingBalance.Amount == 0m;

    public IReadOnlyCollection<ReceivableSettlement> Settlements => _settlements.AsReadOnly();

    /// <summary>
    /// Applies a partial or full payment toward the outstanding balance.
    /// Rejects overpayment per spec: payment > amount due throws.
    /// </summary>
    public void ApplyPayment(Money payment)
    {
        if (payment.Amount <= 0m)
        {
            throw new InvalidOperationException("Payment amount must be greater than zero.");
        }

        if (payment.Amount > OutstandingBalance.Amount)
        {
            throw new InvalidOperationException("Payment amount exceeds outstanding balance.");
        }

        _settlements.Add(new ReceivableSettlement(Id, payment));
        OutstandingBalance = OutstandingBalance - payment;
    }
}
