using POS.Domain.Entities;
using POS.Domain.Exceptions;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class ProductTests
{
    [Fact]
    public void DecreaseStock_WhenQuantityExceedsStock_ThrowsInsufficientStock()
    {
        var product = new Product(
            "Rice",
            Guid.NewGuid(),
            new Money(1_000m),
            new Money(1_500m),
            new StockQuantity(2m),
            new StockQuantity(1m));

        Assert.Throws<InsufficientStockException>(() => product.DecreaseStock(new Quantity(3m)));
    }

    [Fact]
    public void IsLowStock_WhenCurrentStockEqualsMinimum_ReturnsTrue()
    {
        var product = new Product(
            "Beans",
            Guid.NewGuid(),
            new Money(2_000m),
            new Money(2_500m),
            new StockQuantity(5m),
            new StockQuantity(5m));

        Assert.True(product.IsLowStock());
    }

    [Fact]
    public void IsLowStock_WhenStockIsZeroOrMinimumIsZero_ReturnsFalse()
    {
        var outOfStock = new Product(
            "Out of stock",
            Guid.NewGuid(),
            new Money(2_000m),
            new Money(2_500m),
            new StockQuantity(0m),
            new StockQuantity(5m));
        var noMinimum = new Product(
            "No minimum",
            Guid.NewGuid(),
            new Money(2_000m),
            new Money(2_500m),
            new StockQuantity(5m),
            new StockQuantity(0m));

        Assert.False(outOfStock.IsLowStock());
        Assert.False(noMinimum.IsLowStock());
    }

    [Fact]
    public void StockOperations_WhenTracksInventoryIsFalse_IgnoreAdjustments()
    {
        var product = Product.CreateImportedCatalogItem(
            "SRC-01",
            "Non-tracked",
            Guid.NewGuid(),
            new Money(1500m),
            new StockQuantity(0m),
            new StockQuantity(0m),
            POS.Domain.Enums.CatalogItemType.Product,
            false,
            null,
            null);

        product.IncreaseStock(new Quantity(10m));
        Assert.Equal(0m, product.CurrentStock);

        product.DecreaseStock(new Quantity(5m));
        Assert.Equal(0m, product.CurrentStock);
    }
}
