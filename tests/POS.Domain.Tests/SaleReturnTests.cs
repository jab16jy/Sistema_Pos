using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class SaleReturnTests
{
    // --- SaleReturnItem guard tests ---

    [Fact]
    public void SaleReturnItem_QuantityReturnedZero_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SaleReturnItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Rice", 0, new Money(1_000m), new Money(1_000m)));
    }

    [Fact]
    public void SaleReturnItem_QuantityReturnedNegative_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SaleReturnItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Rice", -1, new Money(1_000m), new Money(1_000m)));
    }

    [Fact]
    public void SaleReturnItem_ValidQuantity_Succeeds()
    {
        var item = new SaleReturnItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Rice", 2, new Money(1_500m), new Money(3_000m));

        Assert.Equal(2, item.QuantityReturned);
        Assert.Equal(3_000m, item.RefundAmount.Amount);
    }

    // --- SaleReturn.AddItem and TotalRefund tests ---

    [Fact]
    public void SaleReturn_AddItem_IncludesItemInCollection()
    {
        var saleReturn = new SaleReturn(Guid.NewGuid(), Guid.NewGuid(), "Defective product");
        var item = new SaleReturnItem(saleReturn.Id, Guid.NewGuid(), Guid.NewGuid(), "Widget", 1, new Money(500m), new Money(500m));

        saleReturn.AddItem(item);

        Assert.Single(saleReturn.Items);
    }

    [Fact]
    public void SaleReturn_AddItem_WithWrongReturnId_Throws()
    {
        var saleReturn = new SaleReturn(Guid.NewGuid(), Guid.NewGuid(), "Customer request");
        var itemWithWrongId = new SaleReturnItem(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Widget", 1, new Money(500m), new Money(500m));

        Assert.Throws<InvalidOperationException>(() => saleReturn.AddItem(itemWithWrongId));
    }

    [Fact]
    public void SaleReturn_TotalRefund_SumsAllItemRefunds()
    {
        var saleReturn = new SaleReturn(Guid.NewGuid(), Guid.NewGuid(), "Partial return");
        var productId1 = Guid.NewGuid();
        var productId2 = Guid.NewGuid();

        saleReturn.AddItem(new SaleReturnItem(saleReturn.Id, Guid.NewGuid(), productId1, "Rice", 1, new Money(1_500m), new Money(1_500m)));
        saleReturn.AddItem(new SaleReturnItem(saleReturn.Id, Guid.NewGuid(), productId2, "Sugar", 2, new Money(1_000m), new Money(2_000m)));

        var total = saleReturn.TotalRefund();

        Assert.Equal(3_500m, total.Amount);
    }

    [Fact]
    public void SaleReturn_TotalRefund_WhenNoItems_ReturnsZero()
    {
        var saleReturn = new SaleReturn(Guid.NewGuid(), Guid.NewGuid(), "Full return");

        var total = saleReturn.TotalRefund();

        Assert.Equal(0m, total.Amount);
    }

    [Fact]
    public void SaleReturn_WithOperationId_PreservesTheIdempotencyKey()
    {
        var operationId = Guid.NewGuid();

        var saleReturn = new SaleReturn(Guid.NewGuid(), Guid.NewGuid(), "Partial return", operationId);

        Assert.Equal(operationId, saleReturn.OperationId);
    }

    [Fact]
    public void SaleReturn_WithEmptyOperationId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SaleReturn(Guid.NewGuid(), Guid.NewGuid(), "Partial return", Guid.Empty));
    }

    [Fact]
    public void ReturnSettlement_WithCashAmount_CapturesAnImmutableResolution()
    {
        var returnId = Guid.NewGuid();

        var settlement = new ReturnSettlement(returnId, new Money(3_000m), PaymentMethod.Cash);

        Assert.Equal(returnId, settlement.SaleReturnId);
        Assert.Equal(3_000m, settlement.Amount.Amount);
        Assert.Equal(PaymentMethod.Cash, settlement.PaymentMethod);
        Assert.NotEqual(default, settlement.SettledAt);
    }

    [Theory]
    [InlineData(PaymentMethod.LoyaltyPoints)]
    public void ReturnSettlement_WithUnsupportedPaymentMethod_Throws(PaymentMethod paymentMethod)
    {
        Assert.Throws<ArgumentException>(() =>
            new ReturnSettlement(Guid.NewGuid(), new Money(3_000m), paymentMethod));
    }

    [Fact]
    public void ReturnSettlement_WithZeroAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReturnSettlement(Guid.NewGuid(), Money.Zero, PaymentMethod.Cash));
    }

    [Fact]
    public void ReturnReceivableReconciliation_WithPositiveAmount_CapturesTheReturnAndReceivable()
    {
        var returnId = Guid.NewGuid();
        var receivableId = Guid.NewGuid();

        var reconciliation = new ReturnReceivableReconciliation(returnId, receivableId, new Money(3_000m));

        Assert.Equal(returnId, reconciliation.SaleReturnId);
        Assert.Equal(receivableId, reconciliation.ReceivableId);
        Assert.Equal(3_000m, reconciliation.Amount.Amount);
        Assert.NotEqual(default, reconciliation.ReconciledAt);
    }

    [Fact]
    public void ReturnReceivableReconciliation_WithZeroAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReturnReceivableReconciliation(Guid.NewGuid(), Guid.NewGuid(), Money.Zero));
    }
}
