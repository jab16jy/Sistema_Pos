using POS.Application.DTOs.Inventory;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Inventory;

public sealed class QueryStockUseCase
{
    private readonly IInventoryRepository _inventory;

    public QueryStockUseCase(IInventoryRepository inventory)
    {
        _inventory = inventory;
    }

    public async Task<IReadOnlyList<StockLevelResult>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<InventoryStockLevel> stockLevels = await _inventory.GetStockLevelsAsync(cancellationToken).ConfigureAwait(false);

        return stockLevels
            .Select(stock => new StockLevelResult(
                stock.ProductId,
                stock.ProductName,
                stock.Barcode,
                stock.CurrentStock,
                stock.MinimumStock,
                stock.IsLowStock,
                Math.Max(0m, stock.MinimumStock - stock.CurrentStock)))
            .ToList();
    }
}
