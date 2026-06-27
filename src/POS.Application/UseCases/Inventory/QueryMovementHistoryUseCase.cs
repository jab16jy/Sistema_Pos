using POS.Application.DTOs.Inventory;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Inventory;

public sealed class QueryMovementHistoryUseCase
{
    private readonly IInventoryRepository _inventory;

    public QueryMovementHistoryUseCase(IInventoryRepository inventory)
    {
        _inventory = inventory;
    }

    public async Task<IReadOnlyList<InventoryMovementResult>> ExecuteAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<InventoryMovement> movements = await _inventory
            .GetMovementsByProductAsync(productId, cancellationToken)
            .ConfigureAwait(false);

        return movements
            .OrderByDescending(movement => movement.CreatedAt)
            .Select(ToResult)
            .ToList();
    }

    private static InventoryMovementResult ToResult(InventoryMovement movement) => new(
        movement.Id,
        movement.ProductId,
        movement.Type,
        movement.Quantity,
        movement.Reason,
        movement.UserId,
        movement.ReferenceId,
        movement.CreatedAt);
}
