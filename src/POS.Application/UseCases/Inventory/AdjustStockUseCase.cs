using POS.Application.DTOs.Inventory;
using POS.Application.Services;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Inventory;

public sealed class AdjustStockUseCase
{
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CriticalCommandAuthorizer? _authorizer;

    public AdjustStockUseCase(
        IProductRepository products,
        IInventoryRepository inventory,
        IUnitOfWork unitOfWork,
        CriticalCommandAuthorizer? authorizer = null)
    {
        _products = products;
        _inventory = inventory;
        _unitOfWork = unitOfWork;
        _authorizer = authorizer;
    }

    public async Task<StockLevelResult> ExecuteAsync(AdjustStockRequest request, CancellationToken cancellationToken = default)
    {
        CriticalCommandAuthorizer authorizer = _authorizer
            ?? throw new UnauthorizedActionException(PermissionKeys.Inventory.Adjust);
        CanonicalActor actor = await authorizer.AuthorizeAsync(PermissionKeys.Inventory.Adjust, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("Stock adjustment reason is required.", nameof(request));
        }

        Product product = await _products.GetByIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Product was not found.");

        if (!product.TracksInventory)
        {
            throw new InvalidOperationException("Cannot adjust stock for a product that does not track inventory.");
        }

        InventoryMovement movement = product.RegisterStockMovement(
            InventoryMovementType.Adjustment,
            request.Delta,
            request.Reason,
            actor.UserId);

        await _inventory.AddMovementAsync(movement, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new StockLevelResult(
            product.Id,
            product.Name,
            product.Barcode,
            product.CurrentStock,
            product.MinimumStock,
            product.IsLowStock(),
            Math.Max(0m, product.MinimumStock - product.CurrentStock));
    }
}
