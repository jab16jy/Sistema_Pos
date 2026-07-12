using POS.Application.DTOs.Invoices;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Invoices;

public sealed class ProcessReturnUseCase
{
    private readonly ISaleRepository _sales;
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;
    private readonly ISaleReturnRepository _saleReturns;
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessReturnUseCase(
        ISaleRepository sales,
        IProductRepository products,
        IInventoryRepository inventory,
        ISaleReturnRepository saleReturns,
        IUnitOfWork unitOfWork,
        IUserRepository users)
    {
        _sales = sales;
        _products = products;
        _inventory = inventory;
        _saleReturns = saleReturns;
        _unitOfWork = unitOfWork;
        _users = users;
    }

    public async Task<Guid> ExecuteAsync(ProcessReturnRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProcessedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Processed by user id is required.", nameof(request));
        }

        // R-IR-05: Only administrators can process sale returns
        var user = await _users.GetByIdAsync(request.ProcessedByUserId, cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedActionException("ProcessReturn");

        if (user.Role != UserRole.Administrator)
        {
            throw new UnauthorizedActionException("ProcessReturn");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("Return reason is required.", nameof(request));
        }

        Sale sale = await _sales.GetByIdAsync(request.SaleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Sale was not found.");

        if (await _saleReturns.GetBySaleIdAsync(sale.Id, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException("Sale already returned.");
        }

        sale.MarkReturned();

        var saleReturn = new SaleReturn(sale.Id, request.ProcessedByUserId, request.Reason);

        bool isPartialReturn = request.Items is { Count: > 0 };

        if (isPartialReturn)
        {
            // Partial return — validate and process only the listed items
            await ProcessPartialReturnAsync(request, sale, saleReturn, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Full return — restore all items
            await ProcessFullReturnAsync(request, sale, saleReturn, cancellationToken).ConfigureAwait(false);
        }

        await _saleReturns.AddAsync(saleReturn, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return saleReturn.Id;
    }

    private async Task ProcessPartialReturnAsync(
        ProcessReturnRequest request,
        Sale sale,
        SaleReturn saleReturn,
        CancellationToken cancellationToken)
    {
        foreach (var line in request.Items!)
        {
            var saleItem = sale.Items.FirstOrDefault(i => i.Id == line.SaleItemId)
                ?? throw new InvalidOperationException($"Sale item '{line.SaleItemId}' was not found in sale '{sale.Id}'.");

            if (line.QuantityReturned <= 0 || line.QuantityReturned > (int)saleItem.Quantity)
            {
                throw new InvalidOperationException(
                    $"Quantity returned ({line.QuantityReturned}) for item '{saleItem.ProductName}' must be in [1, {(int)saleItem.Quantity}].");
            }

            Product product = await _products.GetByIdAsync(saleItem.ProductId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Product '{saleItem.ProductId}' was not found.");

            var qty = new Quantity(line.QuantityReturned * saleItem.UnitsPerPresentation);
            if (product.TracksInventory)
            {
                product.IncreaseStock(qty);
                await _inventory.AddMovementAsync(
                    new InventoryMovement(product.Id, InventoryMovementType.Return, qty, "Partial sale return", request.ProcessedByUserId, sale.Id),
                    cancellationToken).ConfigureAwait(false);
            }

            var returnItem = new SaleReturnItem(
                saleReturn.Id,
                saleItem.ProductId,
                saleItem.ProductName,
                line.QuantityReturned,
                saleItem.UnitPrice,
                new Money(line.RefundAmount));

            saleReturn.AddItem(returnItem);
        }
    }

    private async Task ProcessFullReturnAsync(
        ProcessReturnRequest request,
        Sale sale,
        SaleReturn saleReturn,
        CancellationToken cancellationToken)
    {
        foreach (SaleItem item in sale.Items)
        {
            Product product = await _products.GetByIdAsync(item.ProductId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Product '{item.ProductId}' was not found.");

            var quantity = new Quantity(item.Quantity * item.UnitsPerPresentation);
            if (product.TracksInventory)
            {
                product.IncreaseStock(quantity);
                await _inventory.AddMovementAsync(
                    new InventoryMovement(product.Id, InventoryMovementType.Return, quantity, "Full sale return", request.ProcessedByUserId, sale.Id),
                    cancellationToken).ConfigureAwait(false);
            }

            var returnItem = new SaleReturnItem(
                saleReturn.Id,
                item.ProductId,
                item.ProductName,
                (int)item.Quantity,
                item.UnitPrice,
                item.Subtotal());

            saleReturn.AddItem(returnItem);
        }
    }
}
