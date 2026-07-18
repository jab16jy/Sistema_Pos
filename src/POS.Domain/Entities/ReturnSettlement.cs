using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class ReturnSettlement
{
    private ReturnSettlement()
    {
    }

    public ReturnSettlement(Guid saleReturnId, Money amount, PaymentMethod paymentMethod)
    {
        if (saleReturnId == Guid.Empty)
        {
            throw new ArgumentException("Sale return id is required.", nameof(saleReturnId));
        }

        if (amount.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Settlement amount must be greater than zero.");
        }

        if (paymentMethod is not (PaymentMethod.Cash or PaymentMethod.DebitCard or PaymentMethod.CreditCard or PaymentMethod.Transfer))
        {
            throw new ArgumentException("Return settlements require a cash or digital payment method.", nameof(paymentMethod));
        }

        Id = Guid.NewGuid();
        SaleReturnId = saleReturnId;
        Amount = amount;
        PaymentMethod = paymentMethod;
        SettledAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid SaleReturnId { get; private set; }
    public Money Amount { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public DateTimeOffset SettledAt { get; private set; }
}
