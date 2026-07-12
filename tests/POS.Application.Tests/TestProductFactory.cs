using POS.Domain.Entities;
using POS.Domain.ValueObjects;
using POS.Infrastructure.Persistence;

namespace POS.Application.Tests;

internal static class TestProductFactory
{
    public static async Task<Product> CreateAndPersistAsync(
        AppDbContext dbContext,
        string name,
        Money? costPrice,
        Money salePrice,
        StockQuantity currentStock,
        StockQuantity minimumStock,
        CancellationToken cancellationToken,
        Barcode? barcode = null)
    {
        var category = new Category($"Test category {Guid.NewGuid():N}");
        var product = new Product(name, category.Id, costPrice, salePrice, currentStock, minimumStock, barcode: barcode);

        await dbContext.Categories.AddAsync(category, cancellationToken);
        await dbContext.Products.AddAsync(product, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return product;
    }
}
