using POS.Domain.Entities;
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
}
