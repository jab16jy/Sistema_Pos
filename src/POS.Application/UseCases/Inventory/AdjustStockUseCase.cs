using POS.Application.DTOs.Inventory;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Inventory;

public sealed class AdjustStockUseCase
{
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;
    private readonly IUnitOfWork _unitOfWork;

    public AdjustStockUseCase(
        IProductRepository products,
        IInventoryRepository inventory,
        IUnitOfWork unitOfWork)
    {
        _products = products;
        _inventory = inventory;
        _unitOfWork = unitOfWork;
    }

    public async Task<StockLevelResult> ExecuteAsync(AdjustStockRequest request, CancellationToken cancellationToken = default)
    {
        if (request.UserId == Guid.Empty)
        {
            throw new ArgumentException("Stock adjustment requires an operator for audit traceability.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("Stock adjustment reason is required.", nameof(request));
        }

        Product product = await _products.GetByIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Product was not found.");

        InventoryMovement movement = product.RegisterStockMovement(
            InventoryMovementType.Adjustment,
            request.Delta,
            request.Reason,
            request.UserId);

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
