using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class SupplierPurchaseTests
{
    [Fact]
    public void PurchaseItem_Dozens_CalculatesReceivedUnitsCostsAndSubtotal()
    {
        var item = new PurchaseItem(Guid.NewGuid(), "Eggs", "EGG-12", "Docena", 12m, new Quantity(2m), new Money(10_000m));

        Assert.Equal(24m, item.InventoryUnitsReceived);
        Assert.Equal(10_000m, item.PurchaseUnitCost?.Amount);
        Assert.Equal(833.33m, item.BaseUnitCost?.Amount);
        Assert.Equal(20_000m, item.Subtotal?.Amount);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 0)]
    public void PurchaseItem_RejectsNonPositiveQuantityConversionOrCost(decimal quantity, decimal factor, decimal cost)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new PurchaseItem(Guid.NewGuid(), "Rice", null, "Bulto", factor, new Quantity(quantity), new Money(cost)));
    }

    [Fact]
    public void Purchase_StoresAdditionalVatAndAuditableHeaderTotals()
    {
        var purchase = new Purchase(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 7, 11), "FAC-9", "30 days", "Transfer", "received intact", 19m);
        purchase.AddItem(new PurchaseItem(Guid.NewGuid(), "Rice", "R-01", "Sack", 25m, new Quantity(2m), new Money(100_000m)));

        Assert.Equal(PurchaseVatPolicy.Additional, purchase.VatPolicy);
        Assert.Equal(200_000m, purchase.Subtotal?.Amount);
        Assert.Equal(38_000m, purchase.VatAmount?.Amount);
        Assert.Equal(238_000m, purchase.Total?.Amount);
        Assert.Equal("FAC-9", purchase.InvoiceNumber);
    }

    [Fact]
    public void Product_ReceiptCost_UsesWeightedAverageAndDoesNotInventMissingPriorCost()
    {
        Product knownCost = MakeProduct(10m, 100m);
        knownCost.ApplyPurchaseReceiptCost(10m, new Quantity(10m), new Money(200m));
        Assert.Equal(150m, knownCost.CostPrice?.Amount);

        Product unknownCost = MakeProduct(10m, null);
        unknownCost.ApplyPurchaseReceiptCost(10m, new Quantity(10m), new Money(200m));
        Assert.Null(unknownCost.CostPrice);
    }

    [Fact]
    public void SupplierQuotation_Acceptance_DoesNotMutateInventoryAndOnlyAcceptedCanPreload()
    {
        Product product = MakeProduct(3m, 10m);
        var quotation = new SupplierQuotation(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), null, null);
        quotation.AddItem(new SupplierQuotationItem(product.Id, product.Name, "R-1", "Caja", 12m, new Quantity(2m), new Money(24m)));
        quotation.Accept();

        Assert.Equal(SupplierQuotationStatus.Accepted, quotation.Status);
        Assert.Equal(3m, product.CurrentStock);
    }

    private static Product MakeProduct(decimal stock, decimal? cost) => new("Item", Guid.NewGuid(), cost is null ? null : new Money(cost.Value), new Money(500m), new StockQuantity(stock), new StockQuantity(1m));
}
