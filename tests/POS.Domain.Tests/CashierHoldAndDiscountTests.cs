using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class CashierHoldAndDiscountTests
{
    [Fact]
    public void Resume_TransitionsHeldSaleBackToDraftWithoutChangingStock()
    {
        var product = new Product("Coffee", Guid.NewGuid(), new Money(2_000m), new Money(3_000m), new StockQuantity(5m), new StockQuantity(1m));
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(1m), Money.Zero);
        sale.Hold();
        sale.Resume();

        Assert.Equal(SaleStatus.Draft, sale.Status);
        Assert.Equal(4m, product.CurrentStock);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void BusinessSettings_RejectsMaximumCashierDiscountOutsideBounds(decimal maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BusinessSettings(
            "Store", null, null, null, null, null, null, null, false, null, null, null, null, null, "COP", maximum));
    }

    [Theory]
    [InlineData(0)]     // a cap below 1 would reject every tier, since 1x means "no bonus"
    [InlineData(0.5)]
    [InlineData(101)]
    public void BusinessSettings_RejectsMaximumPointsMultiplierOutsideBounds(decimal maximum)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BusinessSettings(
            "Store", null, null, null, null, null, null, null, false, null, null, null, null, null, "COP",
            maximumCashierDiscountPercent: 20m, maximumDeferredBalancePercent: 50m, maximumPointsMultiplier: maximum));
    }

    [Fact]
    public void BusinessSettings_DefaultsMaximumPointsMultiplier()
    {
        var settings = new BusinessSettings(
            "Store", null, null, null, null, null, null, null, false, null, null, null, null, null, "COP");

        Assert.Equal(BusinessSettings.DefaultMaximumPointsMultiplier, settings.MaximumPointsMultiplier);
    }
}
