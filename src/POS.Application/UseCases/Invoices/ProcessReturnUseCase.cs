using POS.Application.DTOs.Invoices;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Invoices;

public sealed class ProcessReturnUseCase
{
    private readonly ISaleRepository _sales;
    private readonly IProductRepository _products;
    private readonly IInventoryRepository _inventory;
    private readonly ISaleReturnRepository _saleReturns;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessReturnUseCase(
        ISaleRepository sales,
        IProductRepository products,
        IInventoryRepository inventory,
        ISaleReturnRepository saleReturns,
        IUnitOfWork unitOfWork)
    {
        _sales = sales;
        _products = products;
        _inventory = inventory;
        _saleReturns = saleReturns;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(ProcessReturnRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProcessedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Processed by user id is required.", nameof(request));
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

        foreach (SaleItem item in sale.Items)
        {
            Product product = await _products.GetByIdAsync(item.ProductId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Product '{item.ProductId}' was not found.");

            var quantity = new Quantity(item.Quantity);
            product.IncreaseStock(quantity);
            await _inventory.AddMovementAsync(
                new InventoryMovement(product.Id, InventoryMovementType.Return, quantity, "Sale return", request.ProcessedByUserId, sale.Id),
                cancellationToken).ConfigureAwait(false);
        }

        var saleReturn = new SaleReturn(sale.Id, request.ProcessedByUserId, request.Reason);
        await _saleReturns.AddAsync(saleReturn, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return saleReturn.Id;
    }
}
