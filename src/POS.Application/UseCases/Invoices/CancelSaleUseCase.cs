using POS.Application.DTOs.Invoices;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Invoices;

public sealed class CancelSaleUseCase
{
    private readonly ISaleRepository _sales;
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;

    public CancelSaleUseCase(
        ISaleRepository sales,
        IProductRepository products,
        IInventoryRepository inventory,
        IUserRepository users,
        IUnitOfWork unitOfWork)
    {
        _sales = sales;
        _products = products;
        _inventory = inventory;
        _users = users;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(CancelSaleRequest request, CancellationToken cancellationToken = default)
    {
        if (request.CancelledByUserId == Guid.Empty)
        {
            throw new ArgumentException("Cancelled by user id is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("Cancellation reason is required.", nameof(request));
        }

        var user = await _users.GetByIdAsync(request.CancelledByUserId, cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedActionException("CancelSale");

        if (user.Role != UserRole.Administrator)
        {
            throw new UnauthorizedActionException("CancelSale");
        }

        Sale sale = await _sales.GetByIdAsync(request.SaleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Sale was not found.");

        sale.MarkCancelled();

        foreach (SaleItem item in sale.Items)
        {
            Product product = await _products.GetByIdAsync(item.ProductId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Product '{item.ProductId}' was not found.");

            var quantity = new Quantity(item.Quantity);
            product.IncreaseStock(quantity);

            await _inventory.AddMovementAsync(
                new InventoryMovement(product.Id, InventoryMovementType.Return, quantity, $"Sale cancellation: {request.Reason}", request.CancelledByUserId, sale.Id),
                cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
