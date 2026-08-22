using POS.Domain.Entities;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class ProductPresentationTests
{
    // Doc example (Ampolla 350): 1 unit = 10.000, but 1 box of 12 = 110.000 (NOT 120.000).
    private static Product MakeAmpolla(decimal stock = 24m, params (string Name, int Units, Money Price)[] presentations) => new(
        "Ampolla 350",
        Guid.NewGuid(),
        new Money(6_000m),          // unit cost
        new Money(10_000m),         // unit price
        new StockQuantity(stock),
        new StockQuantity(0m),
        presentations: presentations.Length > 0
            ? presentations
            : [("Caja", 12, new Money(110_000m))]);

    [Fact]
    public void PackSale_AppliesThePackPrice_NotUnitPriceTimesCount()
    {
        Product product = MakeAmpolla();
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(1m), "Caja", Money.Zero);

        // One box charged at the independent box price, not 12 * 10.000 = 120.000.
        Assert.Equal(110_000m, sale.Total().Amount);
    }

    [Fact]
    public void PackSale_DecrementsStockInBaseUnits()
    {
        Product product = MakeAmpolla(stock: 24m);
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(1m), "Caja", Money.Zero);

        // Selling 1 box of 12 leaves 12 units (doc example).
        Assert.Equal(12m, product.CurrentStock);
    }

    [Fact]
    public void PackSale_CapturesImmutablePresentationAndBaseUnitEquivalence()
    {
        Product product = MakeAmpolla(stock: 24m);
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(1m), "Caja", Money.Zero);

        SaleItem line = Assert.Single(sale.Items);
        Assert.Equal("Caja", line.PresentationName);
        Assert.Equal(12, line.UnitsPerPresentation);
        Assert.Equal(1m, line.Quantity);
    }

    [Fact]
    public void UnitSale_CapturesNoPresentationNameAndOneBaseUnit()
    {
        Product product = MakeAmpolla(stock: 24m);
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(2m), Money.Zero);

        SaleItem line = Assert.Single(sale.Items);
        Assert.Null(line.PresentationName);
        Assert.Equal(1, line.UnitsPerPresentation);
    }

    [Fact]
    public void UnitSale_IsUnaffectedByPackConfig()
    {
        Product product = MakeAmpolla(stock: 24m);
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(2m), Money.Zero);

        Assert.Equal(20_000m, sale.Total().Amount);
        Assert.Equal(22m, product.CurrentStock);
    }

    [Fact]
    public void PackSale_ProfitUsesTheFullPackCost()
    {
        Product product = MakeAmpolla();
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(1m), "Caja", Money.Zero);
        SaleItem line = sale.Items.Single();

        // Cost snapshot for a box = per-unit cost (6.000) * 12 = 72.000; profit = 110.000 - 72.000.
        Assert.Equal(38_000m, line.Profit().Amount);
    }

    [Fact]
    public void SellingAPack_WithoutAnyPresentationConfigured_Throws()
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
            sale.AddItem(product, new Quantity(1m), "Caja", Money.Zero));
    }

    [Fact]
    public void UpdateDetails_CanClearEveryPresentation()
    {
        Product product = MakeAmpolla();

        product.UpdateDetails(
            "Ampolla 350",
            product.CategoryId,
            new Money(6_000m),
            new Money(10_000m),
            new StockQuantity(0m),
            presentations: []);

        Assert.Empty(product.Presentations);
        Assert.False(product.Supports("Caja"));
    }

    [Fact]
    public void PresentationConfig_RequiresAtLeastOneUnit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MakeAmpolla(
            presentations: ("Caja", 0, new Money(110_000m))));
    }

    [Fact]
    public void OneUnitPresentation_UsesItsOwnPrice_DistinctFromTheBaseSalePrice()
    {
        // The client's actual use case: a 1-unit presentation with its own price, e.g. an
        // "Individual especial" sold at 8.000 while the base unit price stays 10.000.
        Product product = MakeAmpolla(
            stock: 24m,
            presentations: ("Individual especial", 1, new Money(8_000m)));

        Assert.True(product.Supports("Individual especial"));
        Assert.Equal(8_000m, product.PriceFor("Individual especial").Amount);
        Assert.NotEqual(product.PriceFor(null).Amount, product.PriceFor("Individual especial").Amount);
        Assert.Equal(1, product.UnitsFor("Individual especial"));
    }

    [Fact]
    public void PresentationConfig_RejectsABlankName()
    {
        Assert.Throws<ArgumentException>(() => MakeAmpolla(
            presentations: ("   ", 12, new Money(110_000m))));
    }

    [Fact]
    public void PresentationConfig_RejectsDuplicateNames_EvenInADifferentCase()
    {
        // Two "Caja" rows at different prices would make a sold line ambiguous.
        Assert.Throws<ArgumentException>(() => MakeAmpolla(
            presentations:
            [
                ("Caja", 12, new Money(110_000m)),
                ("caja", 6, new Money(57_000m))
            ]));
    }

    [Fact]
    public void ShopDefinedPack_UsesItsOwnPriceAndUnits_NotAFractionOfAnyBox()
    {
        // Doc example: half a box is priced at 57.000, deliberately more than 110.000 / 2.
        Product product = MakeAmpolla(
            stock: 24m,
            presentations:
            [
                ("Caja", 12, new Money(110_000m)),
                ("Media caja", 6, new Money(57_000m))
            ]);
        var sale = new Sale(Guid.NewGuid());

        sale.AddItem(product, new Quantity(2m), "Media caja", Money.Zero);

        SaleItem line = Assert.Single(sale.Items);
        Assert.Equal("Media caja", line.PresentationName);
        Assert.Equal(6, line.UnitsPerPresentation);
        Assert.Equal(114_000m, sale.Total().Amount);
        Assert.Equal(12m, product.CurrentStock);
        // Cost snapshot follows the same equivalence: 6.000 per unit * 6 units.
        Assert.Equal(36_000m, line.UnitCost.Amount);
    }

    [Fact]
    public void APresentationNeedNotBeABox_AndStandsOnItsOwn()
    {
        // The whole point of the feature: a pack of 3 that is no fraction of anything.
        Product product = MakeAmpolla(
            stock: 24m,
            presentations: ("Paquete de 3", 3, new Money(28_000m)));

        Assert.True(product.Supports("Paquete de 3"));
        Assert.False(product.Supports("Caja"));
        Assert.Equal(28_000m, product.PriceFor("Paquete de 3").Amount);
        Assert.Equal(9m, product.InventoryUnitsFor("Paquete de 3", 3m));
    }

    [Fact]
    public void PresentationsAreMatchedCaseInsensitively_AndKeepTheOrderTheShopArranged()
    {
        Product product = MakeAmpolla(
            presentations:
            [
                ("Paquete de 5", 5, new Money(45_000m)),
                ("Caja", 12, new Money(110_000m))
            ]);

        Assert.Equal(["Paquete de 5", "Caja"], product.Presentations.Select(p => p.Name));
        Assert.Equal(45_000m, product.PriceFor("paquete DE 5").Amount);
    }

    [Fact]
    public void SellingAPresentation_TheProductDoesNotOffer_Throws()
    {
        Product product = MakeAmpolla();
        var sale = new Sale(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() =>
            sale.AddItem(product, new Quantity(1m), "Media caja", Money.Zero));
    }

    [Fact]
    public void ASoldLine_SurvivesItsPresentationBeingRenamedAndDeleted()
    {
        // The snapshot is a copy, not a foreign key: history must not be rewritten by the catalogue.
        Product product = MakeAmpolla();
        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(1m), "Caja", Money.Zero);

        product.ReplacePresentations([("Cajón", 12, new Money(150_000m))]);
        SaleItem line = Assert.Single(sale.Items);
        Assert.Equal("Caja", line.PresentationName);
        Assert.Equal(12, line.UnitsPerPresentation);
        Assert.Equal(110_000m, line.UnitPrice.Amount);

        product.ReplacePresentations([]);
        Assert.Equal("Caja", line.PresentationName);
        Assert.Equal(110_000m, line.UnitPrice.Amount);
    }
}
