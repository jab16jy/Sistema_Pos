using POS.Application.DTOs.Invoices;
using POS.Application.UseCases.Loyalty;
using POS.Domain.Authorization;
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
    private readonly IReceivableRepository? _receivables;
    private readonly CompensateSaleLoyaltyUseCase? _compensateLoyalty;

    public ProcessReturnUseCase(
        ISaleRepository sales,
        IProductRepository products,
        IInventoryRepository inventory,
        ISaleReturnRepository saleReturns,
        IUnitOfWork unitOfWork,
        IUserRepository users,
        CompensateSaleLoyaltyUseCase? compensateLoyalty = null,
        IReceivableRepository? receivables = null)
    {
        _sales = sales;
        _products = products;
        _inventory = inventory;
        _saleReturns = saleReturns;
        _unitOfWork = unitOfWork;
        _users = users;
        _compensateLoyalty = compensateLoyalty;
        _receivables = receivables;
    }

    public async Task<Guid> ExecuteAsync(ProcessReturnRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProcessedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Processed by user id is required.", nameof(request));
        }

        var user = await _users.GetByIdAsync(request.ProcessedByUserId, cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedActionException("ProcessReturn");

        if (!RoleConstants.IsSystemAdministrator(user.RoleId))
        {
            throw new UnauthorizedActionException("ProcessReturn");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("Return reason is required.", nameof(request));
        }

        Sale sale = await _sales.GetByIdAsync(request.SaleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Sale was not found.");

        if (sale.Status == SaleStatus.Returned)
        {
            throw new InvalidOperationException("Sale already returned.");
        }

        if (sale.Status == SaleStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot return a cancelled sale.");
        }

        // Fetch past returns
        var pastReturns = await _saleReturns.GetAllBySaleIdAsync(sale.Id, cancellationToken).ConfigureAwait(false);
        var pastReturnedQtyBySaleItem = pastReturns
            .SelectMany(r => r.Items)
            .GroupBy(i => i.SaleItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityReturned));

        // Unify return lines
        List<ReturnLineRequest> returnLines;
        if (request.Items is not null)
        {
            if (request.Items.Count == 0)
            {
                throw new InvalidOperationException("Return request must contain at least one item.");
            }
            returnLines = request.Items.ToList();
        }
        else
        {
            returnLines = [];
            foreach (var saleItem in sale.Items)
            {
                int pastReturnedQty = pastReturnedQtyBySaleItem.TryGetValue(saleItem.Id, out int q) ? q : 0;
                int remainingQty = (int)saleItem.Quantity - pastReturnedQty;
                if (remainingQty > 0)
                {
                    decimal refundAmt = Math.Round(remainingQty * (saleItem.Subtotal().Amount / saleItem.Quantity), 2, MidpointRounding.AwayFromZero);
                    returnLines.Add(new ReturnLineRequest(saleItem.Id, remainingQty, refundAmt));
                }
            }
        }

        if (returnLines.Count == 0)
        {
            throw new InvalidOperationException("No items available to return.");
        }

        // Validate all return lines
        HashSet<Guid> returnedItemIds = [];
        foreach (var line in returnLines)
        {
            if (!returnedItemIds.Add(line.SaleItemId))
            {
                throw new InvalidOperationException($"Sale item '{line.SaleItemId}' appears more than once in the return.");
            }

            var saleItem = sale.Items.FirstOrDefault(i => i.Id == line.SaleItemId)
                ?? throw new InvalidOperationException($"Sale item '{line.SaleItemId}' was not found in sale '{sale.Id}'.");

            int pastReturnedQty = pastReturnedQtyBySaleItem.TryGetValue(saleItem.Id, out int q) ? q : 0;
            int remainingQty = (int)saleItem.Quantity - pastReturnedQty;

            if (line.QuantityReturned <= 0 || line.QuantityReturned > remainingQty)
            {
                throw new InvalidOperationException(
                    $"Quantity returned ({line.QuantityReturned}) for item '{saleItem.ProductName}' must be in [1, {remainingQty}].");
            }
        }

        // Create SaleReturn container
        var saleReturn = new SaleReturn(sale.Id, request.ProcessedByUserId, request.Reason);

        // Process inventory & add items to saleReturn
        foreach (var line in returnLines)
        {
            var saleItem = sale.Items.First(i => i.Id == line.SaleItemId);
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
                saleItem.Id,
                saleItem.ProductId,
                saleItem.ProductName,
                line.QuantityReturned,
                saleItem.UnitPrice,
                new Money(line.RefundAmount));

            saleReturn.AddItem(returnItem);
        }

        // Check if 100% of all item quantities have been returned in total
        bool allItemsReturned = true;
        foreach (var saleItem in sale.Items)
        {
            int pastReturnedQty = pastReturnedQtyBySaleItem.TryGetValue(saleItem.Id, out int q) ? q : 0;
            var currentLine = returnLines.FirstOrDefault(l => l.SaleItemId == saleItem.Id);
            int currentReturnedQty = currentLine?.QuantityReturned ?? 0;

            int remainingQty = (int)saleItem.Quantity - (pastReturnedQty + currentReturnedQty);
            if (remainingQty > 0)
            {
                allItemsReturned = false;
            }
        }

        if (allItemsReturned)
        {
            sale.MarkReturned();
        }

        if (_receivables is not null)
        {
            Receivable? receivable = await _receivables.GetBySaleIdAsync(sale.Id, cancellationToken).ConfigureAwait(false);
            if (receivable is not null)
            {
                if (allItemsReturned)
                {
                    receivable.Cancel();
                }
                else
                {
                    receivable.ReduceForReturn(new Money(returnLines.Sum(line => line.RefundAmount)));
                }

                await _receivables.UpdateAsync(receivable, cancellationToken).ConfigureAwait(false);
            }
        }

        // Stage loyalty compensation
        if (_compensateLoyalty is not null)
        {
            var loyaltyLines = returnLines
                .Select(line => new CompensateSaleLoyaltyUseCase.PartialReturnLine(line.SaleItemId, line.QuantityReturned))
                .ToList();
            await _compensateLoyalty.CompensatePartialAsync(sale, loyaltyLines, $"Sale return: {request.Reason}", cancellationToken).ConfigureAwait(false);
        }

        await _saleReturns.AddAsync(saleReturn, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return saleReturn.Id;
    }
}
