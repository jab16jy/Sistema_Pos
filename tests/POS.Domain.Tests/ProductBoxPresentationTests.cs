using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class ProductBoxPresentationTests
{
    // Doc example (Ampolla 350): 1 unit = 10.000, but 1 box of 12 = 110.000 (NOT 120.000).
    private static Product MakeAmpolla(decimal stock = 24m) => new(
        "Ampolla 350",
        Guid.NewGuid(),
        new Money(6_000m),          // unit cost
        new Money(10_000m),         // unit price
        new StockQuantity(stock),
        new StockQuantity(0m),
        unitsPerBox: 12,
        boxPrice: new Money(110_000m));

    [Fact]
    public void BoxSale_AppliesBoxPrice_NotUnitPriceTimesCount()
    {
        Product product = MakeAmpolla();
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(1m), ProductPresentation.Box, Money.Zero);

        // One box charged at the independent box price, not 12 * 10.000 = 120.000.
        Assert.Equal(110_000m, sale.Total().Amount);
    }

    [Fact]
    public void BoxSale_DecrementsStockInBaseUnits()
    {
        Product product = MakeAmpolla(stock: 24m);
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(1m), ProductPresentation.Box, Money.Zero);

        // Selling 1 box of 12 leaves 12 units (doc example).
        Assert.Equal(12m, product.CurrentStock);
    }

    [Fact]
    public void BoxSale_CapturesImmutablePresentationAndBaseUnitEquivalence()
    {
        Product product = MakeAmpolla(stock: 24m);
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(1m), ProductPresentation.Box, Money.Zero);

        SaleItem line = Assert.Single(sale.Items);
        Assert.Equal(ProductPresentation.Box, line.Presentation);
        Assert.Equal(12, line.UnitsPerPresentation);
        Assert.Equal(1m, line.Quantity);
    }

    [Fact]
    public void UnitSale_CapturesUnitPresentationWithOneBaseUnit()
    {
        Product product = MakeAmpolla(stock: 24m);
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(2m), ProductPresentation.Unit, Money.Zero);

        SaleItem line = Assert.Single(sale.Items);
        Assert.Equal(ProductPresentation.Unit, line.Presentation);
        Assert.Equal(1, line.UnitsPerPresentation);
    }

    [Fact]
    public void UnitSale_IsUnaffectedByBoxConfig()
    {
        Product product = MakeAmpolla(stock: 24m);
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(2m), ProductPresentation.Unit, Money.Zero);

        Assert.Equal(20_000m, sale.Total().Amount);
        Assert.Equal(22m, product.CurrentStock);
    }

    [Fact]
    public void BoxSale_ProfitUsesFullBoxCost()
    {
        Product product = MakeAmpolla();
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(1m), ProductPresentation.Box, Money.Zero);
        SaleItem line = sale.Items.Single();

        // Cost snapshot for a box = per-unit cost (6.000) * 12 = 72.000; profit = 110.000 - 72.000.
        Assert.Equal(38_000m, line.Profit().Amount);
    }

    [Fact]
    public void SellingBox_WithoutBoxConfig_Throws()
    {
        var product = new Product(
            "Café",
            Guid.NewGuid(),
            new Money(2_000m),
            new Money(3_000m),
            new StockQuantity(10m),
            new StockQuantity(0m));
        var sale = new Sale(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            sale.AddItem(product, new Quantity(1m), ProductPresentation.Box, Money.Zero));
    }

    [Fact]
    public void UpdateDetails_CanClearBoxConfig()
    {
        Product product = MakeAmpolla();

        product.UpdateDetails(
            "Ampolla 350",
            product.CategoryId,
            new Money(6_000m),
            new Money(10_000m),
            new StockQuantity(0m),
            unitsPerBox: null,
            boxPrice: null);

        Assert.False(product.SupportsBoxSale);
        Assert.Null(product.UnitsPerBox);
        Assert.Null(product.BoxPrice);
    }

    [Fact]
    public void BoxConfig_RequiresAtLeastTwoUnits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Product(
            "Ampolla 350",
            Guid.NewGuid(),
            new Money(6_000m),
            new Money(10_000m),
            new StockQuantity(24m),
            new StockQuantity(0m),
            unitsPerBox: 1,
            boxPrice: new Money(110_000m)));
    }

    [Fact]
    public void BoxConfig_RequiresBothUnitsAndPrice()
    {
        Assert.Throws<ArgumentException>(() => new Product(
            "Ampolla 350",
            Guid.NewGuid(),
            new Money(6_000m),
            new Money(10_000m),
            new StockQuantity(24m),
            new StockQuantity(0m),
            unitsPerBox: 12,
            boxPrice: null));
    }
}
