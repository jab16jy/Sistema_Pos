using POS.Application.DTOs.Invoices;
using POS.Application.Services;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly CompensateSaleLoyaltyUseCase? _compensateLoyalty;
    private readonly IReceivableRepository? _receivables;
    private readonly CriticalCommandAuthorizer? _authorizer;

    public ProcessReturnUseCase(
        ISaleRepository sales,
        IProductRepository products,
        IInventoryRepository inventory,
        ISaleReturnRepository saleReturns,
        IUnitOfWork unitOfWork,
        IUserRepository users,
        CompensateSaleLoyaltyUseCase? compensateLoyalty = null,
        IReceivableRepository? receivables = null,
        CriticalCommandAuthorizer? authorizer = null)
    {
        _sales = sales;
        _products = products;
        _inventory = inventory;
        _saleReturns = saleReturns;
        _unitOfWork = unitOfWork;
        _compensateLoyalty = compensateLoyalty;
        _receivables = receivables;
        _authorizer = authorizer;
    }

    public async Task<Guid> ExecuteAsync(ProcessReturnRequest request, CancellationToken cancellationToken = default)
    {
        CriticalCommandAuthorizer authorizer = _authorizer
            ?? throw new UnauthorizedActionException(PermissionKeys.Invoices.Return);
        CanonicalActor actor = await authorizer.AuthorizeAsync(PermissionKeys.Invoices.Return, cancellationToken).ConfigureAwait(false);

        Guid saleReturnId = Guid.Empty;
        await _unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            saleReturnId = await ExecuteInTransactionAsync(request, actor, transactionCancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        return saleReturnId;
    }

    private async Task<Guid> ExecuteInTransactionAsync(ProcessReturnRequest request, CanonicalActor actor, CancellationToken cancellationToken)
    {
        if (request.OperationId == Guid.Empty)
        {
            throw new ArgumentException("Return operation id is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("Return reason is required.", nameof(request));
        }

        SaleReturn? existing = await _saleReturns.GetByOperationIdAsync(request.OperationId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.Id;
        }

        Sale sale = await _sales.GetByIdAsync(request.SaleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Sale was not found.");

        if (sale.Status != SaleStatus.Completed)
        {
            throw new InvalidOperationException("Only completed sales can be returned.");
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
                    returnLines.Add(new ReturnLineRequest(saleItem.Id, remainingQty));
                }
            }
        }

        if (returnLines.Count == 0)
        {
            throw new InvalidOperationException("No items available to return.");
        }

        var validatedLines = new List<(ReturnLineRequest Line, SaleItem SaleItem, Product Product, Money Refund)>();
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

            Product product = await _products.GetByIdAsync(saleItem.ProductId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Product '{saleItem.ProductId}' was not found.");
            validatedLines.Add((line, saleItem, product, DeriveRefund(saleItem, line.QuantityReturned)));
        }

        Money totalRefund = new(validatedLines.Sum(line => line.Refund.Amount));
        (PaymentMethod? paymentMethod, Receivable? receivable) = await ResolveAsync(
            request.ResolutionChoice, sale, totalRefund, cancellationToken).ConfigureAwait(false);
        var saleReturn = new SaleReturn(sale.Id, actor.UserId, request.Reason, request.OperationId);

        foreach (var (line, saleItem, product, refund) in validatedLines)
        {
            var qty = new Quantity(line.QuantityReturned * saleItem.UnitsPerPresentation);
            if (product.TracksInventory)
            {
                product.IncreaseStock(qty);
                await _inventory.AddMovementAsync(
                    new InventoryMovement(product.Id, InventoryMovementType.Return, qty, "Partial sale return", actor.UserId, sale.Id),
                    cancellationToken).ConfigureAwait(false);
            }

            var returnItem = new SaleReturnItem(
                saleReturn.Id,
                saleItem.Id,
                saleItem.ProductId,
                saleItem.ProductName,
                line.QuantityReturned,
                saleItem.UnitPrice,
                refund);

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

        await StageResolutionAsync(saleReturn, totalRefund, paymentMethod, receivable, cancellationToken).ConfigureAwait(false);
        await _saleReturns.AddAsync(saleReturn, cancellationToken).ConfigureAwait(false);
        if (_compensateLoyalty is not null)
        {
            await _compensateLoyalty.CompensateReturnAsync(
                sale, pastReturns, saleReturn, $"Sale return: {request.Reason}", cancellationToken).ConfigureAwait(false);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return saleReturn.Id;
    }

    private async Task<(PaymentMethod? paymentMethod, Receivable? receivable)> ResolveAsync(
        ReturnResolutionChoice choice,
        Sale sale,
        Money amount,
        CancellationToken cancellationToken)
    {
        IReceivableRepository receivables = _receivables
            ?? throw new InvalidOperationException("Return resolution requires receivable support.");

        PaymentMethod? paymentMethod = choice switch
        {
            ReturnResolutionChoice.Cash => PaymentMethod.Cash,
            ReturnResolutionChoice.DebitCard => PaymentMethod.DebitCard,
            ReturnResolutionChoice.CreditCard => PaymentMethod.CreditCard,
            ReturnResolutionChoice.Transfer => PaymentMethod.Transfer,
            ReturnResolutionChoice.Receivable => null,
            _ => throw new ArgumentOutOfRangeException(nameof(choice), "Return resolution choice is invalid.")
        };

        if (paymentMethod is not null)
        {
            return (paymentMethod, null);
        }

        Receivable receivable = await receivables.GetBySaleIdAsync(sale.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The originating sale has no receivable to reconcile.");

        if (sale.CustomerId is not Guid customerId ||
            receivable.SaleId != sale.Id ||
            receivable.CustomerId != customerId ||
            receivable.IsCancelled ||
            amount.Amount > receivable.OutstandingBalance.Amount)
        {
            throw new InvalidOperationException("The receivable cannot reconcile this return.");
        }

        return (null, receivable);
    }

    private async Task StageResolutionAsync(
        SaleReturn saleReturn,
        Money amount,
        PaymentMethod? paymentMethod,
        Receivable? receivable,
        CancellationToken cancellationToken)
    {
        IReceivableRepository receivables = _receivables
            ?? throw new InvalidOperationException("Return resolution requires receivable support.");

        if (paymentMethod is not null)
        {
            await receivables.AddReturnSettlementAsync(
                new ReturnSettlement(saleReturn.Id, amount, paymentMethod.Value), cancellationToken).ConfigureAwait(false);
            return;
        }

        if (receivable is null)
        {
            throw new InvalidOperationException("The receivable cannot reconcile this return.");
        }

        receivable.ReduceForReturn(amount);
        await receivables.UpdateAsync(receivable, cancellationToken).ConfigureAwait(false);
        await receivables.AddReturnReceivableReconciliationAsync(
            new ReturnReceivableReconciliation(saleReturn.Id, receivable.Id, amount), cancellationToken).ConfigureAwait(false);
    }

    private static Money DeriveRefund(SaleItem saleItem, int quantityReturned)
    {
        if (saleItem.Quantity <= 0m)
        {
            throw new InvalidOperationException($"Sale item '{saleItem.Id}' has an invalid quantity snapshot.");
        }

        decimal amount = saleItem.UnitPrice.Amount * quantityReturned
            - (saleItem.Discount.Amount * quantityReturned / saleItem.Quantity);
        if (amount < 0m)
        {
            throw new InvalidOperationException($"Derived refund for item '{saleItem.Id}' cannot be negative.");
        }

        return new Money(amount);
    }
}
