using POS.Application.DTOs.Invoices;
using POS.Application.UseCases.Auth;
using POS.Application.UseCases.Loyalty;
using POS.Domain.Authorization;
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
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IReceivableRepository? _receivables;
    private readonly CompensateSaleLoyaltyUseCase? _compensateLoyalty;

    public CancelSaleUseCase(
        ISaleRepository sales,
        IProductRepository products,
        IInventoryRepository inventory,
        IUserRepository users,
        IRoleRepository roles,
        IUnitOfWork unitOfWork,
        CompensateSaleLoyaltyUseCase? compensateLoyalty = null,
        IReceivableRepository? receivables = null)
    {
        _sales = sales;
        _products = products;
        _inventory = inventory;
        _users = users;
        _roles = roles;
        _unitOfWork = unitOfWork;
        _compensateLoyalty = compensateLoyalty;
        _receivables = receivables;
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

        string? unauthorized = await PermissionAuthorization.EnsureHasPermissionAsync(
            _users, _roles, request.CancelledByUserId, PermissionKeys.Invoices.Cancel,
            "No tienes permiso para anular facturas.", cancellationToken).ConfigureAwait(false);
        if (unauthorized is not null)
        {
            throw new UnauthorizedActionException("CancelSale");
        }

        Sale sale = await _sales.GetByIdAsync(request.SaleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Sale was not found.");

        sale.MarkCancelled();

        if (_receivables is not null)
        {
            Receivable? receivable = await _receivables.GetBySaleIdAsync(sale.Id, cancellationToken).ConfigureAwait(false);
            if (receivable is not null)
            {
                receivable.Cancel();
                await _receivables.UpdateAsync(receivable, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (SaleItem item in sale.Items)
        {
            Product product = await _products.GetByIdAsync(item.ProductId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Product '{item.ProductId}' was not found.");

            if (product.TracksInventory)
            {
                var quantity = new Quantity(item.Quantity * item.UnitsPerPresentation);
                product.IncreaseStock(quantity);

                await _inventory.AddMovementAsync(
                    new InventoryMovement(product.Id, InventoryMovementType.Return, quantity, $"Sale cancellation: {request.Reason}", request.CancelledByUserId, sale.Id),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (_compensateLoyalty is not null)
        {
            await _compensateLoyalty.CompensateFullAsync(sale, $"Sale cancellation: {request.Reason}", cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
