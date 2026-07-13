using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class LoyaltyTests
{
    // --- LoyaltyRule tests ---

    [Fact]
    public void LoyaltyRule_PointsFor_AppliesFloorRounding()
    {
        // 1 pt per 1,000 COP: PointsPerCurrencyUnit = 0.001
        var rule = new LoyaltyRule(0.001m, 1m, 0);

        Assert.Equal(5, rule.PointsFor(new Money(5_000m)));   // exactly 5
        Assert.Equal(4, rule.PointsFor(new Money(4_999m)));   // FLOOR → 4
        Assert.Equal(0, rule.PointsFor(new Money(999m)));     // below threshold
        Assert.Equal(1, rule.PointsFor(new Money(1_000m)));   // exactly 1
    }

    [Fact]
    public void LoyaltyRule_DiscountFor_CalculatesCopValue()
    {
        var rule = new LoyaltyRule(0.001m, 1000m, 0);

        var discount = rule.DiscountFor(5);

        Assert.Equal(5_000m, discount.Amount);
    }

    [Fact]
    public void LoyaltyRule_DiscountFor_ZeroPoints_ReturnsZero()
    {
        var rule = new LoyaltyRule(0.001m, 1m, 0);

        var discount = rule.DiscountFor(0);

        Assert.Equal(0m, discount.Amount);
    }

    [Fact]
    public void LoyaltyRule_CtorGuard_ThrowsWhenPointsPerUnitIsZero()
    {
        Assert.Throws<ArgumentException>(() => new LoyaltyRule(0m, 1m, 0));
    }

    [Fact]
    public void LoyaltyRule_CtorGuard_ThrowsWhenPointsPerUnitIsNegative()
    {
        Assert.Throws<ArgumentException>(() => new LoyaltyRule(-0.001m, 1m, 0));
    }

    // --- LoyaltyTransaction ctor guard tests ---

    [Fact]
    public void LoyaltyTransaction_EarnedType_WithNegativePoints_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new LoyaltyTransaction(Guid.NewGuid(), LoyaltyTransactionType.Earned, -5, null));
    }

    [Fact]
    public void LoyaltyTransaction_EarnedType_WithZeroPoints_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new LoyaltyTransaction(Guid.NewGuid(), LoyaltyTransactionType.Earned, 0, null));
    }

    [Fact]
    public void LoyaltyTransaction_RedeemedType_WithPositivePoints_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new LoyaltyTransaction(Guid.NewGuid(), LoyaltyTransactionType.Redeemed, 5, null));
    }

    [Fact]
    public void LoyaltyTransaction_EarnedType_WithPositivePoints_Succeeds()
    {
        var accountId = Guid.NewGuid();
        var tx = new LoyaltyTransaction(accountId, LoyaltyTransactionType.Earned, 10, null);

        Assert.Equal(accountId, tx.LoyaltyAccountId);
        Assert.Equal(10, tx.Points);
        Assert.Equal(LoyaltyTransactionType.Earned, tx.Type);
    }

    [Fact]
    public void LoyaltyTransaction_RedeemedType_WithNegativePoints_Succeeds()
    {
        var accountId = Guid.NewGuid();
        var tx = new LoyaltyTransaction(accountId, LoyaltyTransactionType.Redeemed, -10, null);

        Assert.Equal(-10, tx.Points);
        Assert.Equal(LoyaltyTransactionType.Redeemed, tx.Type);
    }

    [Theory]
    [InlineData(LoyaltyTransactionType.EarnReversed, 1)]
    [InlineData(LoyaltyTransactionType.RedemptionRestored, -1)]
    [InlineData(LoyaltyTransactionType.Earned, 0)]
    [InlineData(LoyaltyTransactionType.Redeemed, 0)]
    public void LoyaltyTransaction_RejectsInvalidTypeAndSign(LoyaltyTransactionType type, int points)
    {
        Assert.Throws<ArgumentException>(() => new LoyaltyTransaction(Guid.NewGuid(), type, points, Guid.NewGuid()));
    }

    [Fact]
    public void LoyaltyTransaction_CreateCompensation_PreservesSourceSaleAndLinksSource()
    {
        var source = new LoyaltyTransaction(Guid.NewGuid(), LoyaltyTransactionType.Redeemed, -25, Guid.NewGuid());

        LoyaltyTransaction compensation = LoyaltyTransaction.CreateCompensation(source, "Sale return", pointsToCompensate: 10);

        Assert.Equal(LoyaltyTransactionType.RedemptionRestored, compensation.Type);
        Assert.Equal(10, compensation.Points);
        Assert.Equal(source.Id, compensation.CompensatesTransactionId);
        Assert.Equal(source.SaleId, compensation.SaleId);
    }

    [Fact]
    public void Sale_EffectiveEarnableTotal_ExcludesLoyaltyPointsTender()
    {
        var product = new Product("Rice", Guid.NewGuid(), new Money(500m), new Money(1_000m), new StockQuantity(10m), new StockQuantity(1m));
        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(10m), new Money(0m));
        sale.AddPayment(new Payment(PaymentMethod.LoyaltyPoints, new Money(1_000m)));

        Assert.Equal(1_000m, sale.LoyaltyPointsTenderTotal().Amount);
        Assert.Equal(9_000m, sale.EffectiveEarnableTotal().Amount);
    }

    [Fact]
    public void LoyaltyTier_Update_ChangesThresholdAndBenefits()
    {
        var tier = new LoyaltyTier("Bronce", 0, 0, 1m);

        tier.Update("Oro", 1_000, 12.5m, 1.5m);

        Assert.Equal("Oro", tier.Name);
        Assert.Equal(1_000, tier.MinPoints);
        Assert.Equal(12.5m, tier.DiscountPercent);
        Assert.Equal(1.5m, tier.PointsMultiplier);
    }

    [Fact]
    public void LoyaltyTier_Update_RejectsInvalidColor()
    {
        var tier = new LoyaltyTier("Bronce", 0, 0, 1m);

        Assert.Throws<ArgumentException>(() => tier.Update("Oro", 1_000, 12.5m, 1.5m, "not-a-color"));
    }

    [Fact]
    public void LoyaltyTier_Update_AcceptsValidColor()
    {
        var tier = new LoyaltyTier("Bronce", 0, 0, 1m);

        tier.Update("Oro", 1_000, 12.5m, 1.5m, "#FFAA00");

        Assert.Equal("#FFAA00", tier.Color);
    }

    [Fact]
    public void LoyaltyTier_DefaultsTextForegroundToWhite_AndRejectsUnsupportedValue()
    {
        var tier = new LoyaltyTier("Bronce", 0);

        Assert.Equal(LoyaltyTierTextForeground.White, tier.TextForeground);
        Assert.Throws<ArgumentOutOfRangeException>(() => tier.Update("Bronce", 0, 0, 1m, "#64748B", (LoyaltyTierTextForeground)99));
    }

    [Theory]
    [InlineData(-1, 0, 1)]
    [InlineData(0, 101, 1)]
    [InlineData(0, 0, 0)]
    public void LoyaltyTier_Ctor_RejectsInvalidBenefits(decimal minPoints, decimal discountPercent, decimal multiplier)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new LoyaltyTier("Tier", minPoints, discountPercent, multiplier));
    }
}
