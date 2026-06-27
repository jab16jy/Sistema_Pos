using POS.Application.DTOs.Inventory;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Inventory;

public sealed class QueryLowStockUseCase
{
    private readonly IInventoryRepository _inventory;

    public QueryLowStockUseCase(IInventoryRepository inventory)
    {
        _inventory = inventory;
    }

    public async Task<IReadOnlyList<LowStockProductResult>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Product> products = await _inventory.GetLowStockProductsAsync(cancellationToken).ConfigureAwait(false);

        return products
            .Where(product => product.MinimumStock > 0m)
            .Select(product => new LowStockProductResult(
                product.Id,
                product.Name,
                product.Barcode,
                product.CurrentStock,
                product.MinimumStock,
                Math.Max(0m, product.MinimumStock - product.CurrentStock)))
            .ToList();
    }
}
