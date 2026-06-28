using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class SaleTests
{
    [Fact]
    public void Complete_WhenPaymentCoversTotal_CompletesSaleAndSeparatesCash()
    {
        var product = new Product(
            "Coffee",
            Guid.NewGuid(),
            new Money(2_000m),
            new Money(3_000m),
            new StockQuantity(10m),
            new StockQuantity(2m));

        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(2m), Money.Zero);
        sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(6_000m), new Money(10_000m)));

        sale.Complete();

        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(6_000m, sale.Total().Amount);
        Assert.Equal(6_000m, sale.CashInDrawer().Amount);
        Assert.Equal(8m, product.CurrentStock);
    }

    // --- Settlement plan tests (RED) ---

    [Fact]
    public void Complete_WithSettlementPlan_WhenCollectedPlusDeferredEqualsTotal_CompletesSuccessfully()
    {
        var product = MakeProduct(10_000m, stock: 5m);
        var customerId = Guid.NewGuid();
        var sale = new Sale(Guid.NewGuid(), customerId);
        sale.AddItem(product, new Quantity(1m), Money.Zero);

        // Collected 6000, deferred 4000 = total 10000
        sale.SetSettlementPlan(new SaleSettlementPlan(6_000m, PaymentMethod.Cash, 4_000m));
        sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(6_000m), new Money(6_000m)));

        sale.Complete();

        Assert.Equal(SaleStatus.Completed, sale.Status);
        Assert.Equal(4_000m, sale.DeferredBalance().Amount);
        Assert.Equal(6_000m, sale.CashInDrawer().Amount);
    }

    [Fact]
    public void Complete_WithDeferredBalance_WhenNoCustomer_Throws()
    {
        var product = MakeProduct(10_000m, stock: 5m);
        var sale = new Sale(Guid.NewGuid()); // no customer
        sale.AddItem(product, new Quantity(1m), Money.Zero);

        // Deferred balance without a customer is forbidden (spec: reject orphan receivable)
        sale.SetSettlementPlan(new SaleSettlementPlan(6_000m, PaymentMethod.Cash, 4_000m));
        sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(6_000m), new Money(6_000m)));

        Assert.Throws<InvalidOperationException>(() => sale.Complete());
    }

    [Fact]
    public void DeferredBalance_WhenNoSettlementPlan_ReturnsZero()
    {
        var product = MakeProduct(5_000m, stock: 5m);
        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(1m), Money.Zero);
        sale.AddPayment(new Payment(PaymentMethod.Cash, new Money(5_000m), new Money(5_000m)));
        sale.Complete();

        Assert.Equal(Money.Zero, sale.DeferredBalance());
    }

    [Fact]
    public void AddItem_CapturesUnitCostFromProductCostPrice()
    {
        var costPrice = new Money(2_000m);
        var product = new Product(
            "Coffee",
            Guid.NewGuid(),
            costPrice,
            new Money(3_000m),
            new StockQuantity(10m),
            new StockQuantity(2m));

        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(1m), Money.Zero);

        var item = sale.Items.Single();
        Assert.Equal(2_000m, item.UnitCost.Amount);
    }

    [Fact]
    public void AddItem_CapturesTaxRateAndCalculatesIncludedIva()
    {
        var product = new Product(
            "Coffee",
            Guid.NewGuid(),
            new Money(2_000m),
            new Money(11_900m),
            new StockQuantity(10m),
            new StockQuantity(2m),
            taxRate: 19m);

        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(1m), Money.Zero);

        var item = sale.Items.Single();
        Assert.Equal(19m, item.TaxRate);
        Assert.Equal(1_900m, item.TaxAmount().Amount);
        Assert.Equal(10_000m, item.TaxableBaseAmount().Amount);
        Assert.Equal(1_900m, sale.TaxTotal().Amount);
        Assert.Equal(10_000m, sale.TaxableBaseTotal().Amount);
        Assert.Equal(11_900m, sale.Total().Amount);
    }

    [Fact]
    public void SaleItem_Profit_CalculatesCorrectly()
    {
        var product = new Product(
            "Widget",
            Guid.NewGuid(),
            new Money(1_000m),  // cost
            new Money(1_500m),  // sale price
            new StockQuantity(10m),
            new StockQuantity(2m));

        var sale = new Sale(Guid.NewGuid());
        sale.AddItem(product, new Quantity(3m), Money.Zero);

        var item = sale.Items.Single();
        // Profit = (1500 - 1000) * 3 - 0 = 1500
        Assert.Equal(1_500m, item.Profit().Amount);
    }

    private static Product MakeProduct(decimal salePrice, decimal stock = 10m) =>
        new Product("Item", Guid.NewGuid(), new Money(salePrice / 2), new Money(salePrice),
            new StockQuantity(stock), new StockQuantity(1m));
}
