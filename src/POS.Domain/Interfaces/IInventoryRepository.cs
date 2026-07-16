using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface IInventoryRepository
{
    Task<IReadOnlyList<InventoryStockLevel>> GetStockLevelsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryMovement>> GetMovementsByProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default);
    Task AddMovementAsync(InventoryMovement movement, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryMovement>> GetMovementsByReferenceIdAsync(Guid referenceId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<InventoryMovement>>(System.Array.Empty<InventoryMovement>());
}

public sealed record InventoryStockLevel(
    Guid ProductId,
    string ProductName,
    string? Barcode,
    decimal CurrentStock,
    decimal MinimumStock,
    bool IsLowStock);
