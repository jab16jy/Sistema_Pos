using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Sales;

public sealed class CancelHeldSaleUseCase
{
    private readonly ISaleRepository _sales;
    private readonly IProductRepository _products;
    private readonly IInventoryRepository? _inventory;
    private readonly IUnitOfWork _unitOfWork;

    public CancelHeldSaleUseCase(
        ISaleRepository sales,
        IProductRepository products,
        IUnitOfWork unitOfWork,
        IInventoryRepository? inventory = null)
    {
        _sales = sales;
        _products = products;
        _unitOfWork = unitOfWork;
        _inventory = inventory;
    }

    public async Task ExecuteAsync(
        Guid saleId,
        Guid cashierUserId,
        CancellationToken cancellationToken = default)
    {
        Sale sale = await _sales.GetByIdAsync(saleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The held sale was not found.");

        if (sale.CashierUserId != cashierUserId)
        {
            throw new UnauthorizedAccessException("The held sale belongs to another cashier.");
        }

        if (sale.Status != SaleStatus.OnHold)
        {
            throw new InvalidOperationException("Only held sales can be cancelled this way.");
        }

        // Restore stock and record a compensating movement for each line.
        foreach (SaleItem item in sale.Items)
        {
            Product product = await _products.GetByIdAsync(item.ProductId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Product '{item.ProductId}' was not found.");

            if (product.TracksInventory)
            {
                var quantity = new Quantity(item.Quantity * item.UnitsPerPresentation);
                product.IncreaseStock(quantity);

                if (_inventory is not null)
                {
                    await _inventory.AddMovementAsync(
                        new InventoryMovement(
                            product.Id,
                            InventoryMovementType.Return,
                            quantity,
                            "Venta en espera cancelada",
                            cashierUserId,
                            sale.Id),
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }

        sale.MarkCancelled();
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
