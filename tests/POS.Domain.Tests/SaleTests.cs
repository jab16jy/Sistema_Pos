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
}
