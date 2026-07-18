using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class ReceivableTests
{
    [Fact]
    public void ApplyPayment_PersistsTenderAndReducesBalance()
    {
        var receivable = new Receivable(Guid.NewGuid(), Guid.NewGuid(), new Money(50_000m));

        receivable.ApplyPayment(new Money(20_000m), PaymentMethod.Transfer, "Nequi");

        var settlement = Assert.Single(receivable.Settlements);
        Assert.Equal(PaymentMethod.Transfer, settlement.Method);
        Assert.Equal("Nequi", settlement.BankOrPlatform);
        Assert.Equal(30_000m, receivable.OutstandingBalance.Amount);
    }

    [Fact]
    public void Cancel_ClosesBalanceAndRejectsLaterPayments()
    {
        var receivable = new Receivable(Guid.NewGuid(), Guid.NewGuid(), new Money(50_000m));

        receivable.Cancel();

        Assert.True(receivable.IsCancelled);
        Assert.False(receivable.IsPaidOff);
        Assert.Equal(0m, receivable.OutstandingBalance.Amount);
        Assert.Throws<InvalidOperationException>(() => receivable.ApplyPayment(new Money(1m)));
    }

    [Fact]
    public void ReduceForReturn_ExceedingOutstandingBalance_IsRejected()
    {
        var receivable = new Receivable(Guid.NewGuid(), Guid.NewGuid(), new Money(10_000m));

        Assert.Throws<InvalidOperationException>(() => receivable.ReduceForReturn(new Money(20_000m)));
        Assert.Equal(10_000m, receivable.OutstandingBalance.Amount);
    }

    [Fact]
    public void ReduceForReturn_MatchingOutstandingBalance_PaysOff()
    {
        var receivable = new Receivable(Guid.NewGuid(), Guid.NewGuid(), new Money(10_000m));

        receivable.ReduceForReturn(new Money(10_000m));

        Assert.Equal(0m, receivable.OutstandingBalance.Amount);
        Assert.True(receivable.IsPaidOff);
    }
}
