using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class Payment
{
    private Payment()
    {
    }

    public Payment(PaymentMethod method, Money amount, Money? receivedAmount = null, string? bankOrPlatform = null)
    {
        Id = Guid.NewGuid();
        Method = method;
        Amount = amount;
        ReceivedAmount = receivedAmount;
        BankOrPlatform = bankOrPlatform?.Trim();

        if (method == PaymentMethod.Cash && receivedAmount is not null && receivedAmount.Value.Amount < amount.Amount)
        {
            throw new InvalidOperationException("Cash received amount cannot be lower than payment amount.");
        }
    }

    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public Money Amount { get; private set; }
    public Money? ReceivedAmount { get; private set; }
    public string? BankOrPlatform { get; private set; }

    public Money ChangeAmount()
    {
        if (Method != PaymentMethod.Cash || ReceivedAmount is null)
        {
            return Money.Zero;
        }

        return new Money(ReceivedAmount.Value.Amount - Amount.Amount);
    }
}
