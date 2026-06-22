using POS.Application.DTOs.Purchases;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Purchases;

public class RegisterPurchaseUseCase
{
    private readonly ISupplierRepository _suppliers;
    private readonly IProductRepository _products;
    private readonly IPurchaseRepository _purchases;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterPurchaseUseCase(
        ISupplierRepository suppliers,
        IProductRepository products,
        IPurchaseRepository purchases,
        IUnitOfWork unitOfWork)
    {
        _suppliers = suppliers;
        _products = products;
        _purchases = purchases;
        _unitOfWork = unitOfWork;
    }

    public virtual async Task<RegisterPurchaseResult> ExecuteAsync(RegisterPurchaseRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
        {
            throw new ArgumentException("Purchase must contain at least one item.", nameof(request));
        }

        if (request.UserId == Guid.Empty)
        {
            throw new ArgumentException("Purchase registration requires an operator for audit traceability.", nameof(request));
        }

        Supplier supplier = await _suppliers.GetByIdAsync(request.SupplierId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Supplier is not available for purchase selection.");
        if (!supplier.IsActive)
        {
            throw new InvalidOperationException("Supplier is not available for purchase selection.");
        }

        var purchase = new Purchase(supplier.Id, request.UserId);
        var movements = new List<InventoryMovement>();
        decimal total = 0m;

        foreach (RegisterPurchaseItemRequest item in request.Items)
        {
            Product product = await _products.GetByIdAsync(item.ProductId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Product was not found for purchase registration.");
            var quantity = new Quantity(item.Quantity);
            var totalCost = new Money(item.TotalCost);
            purchase.AddItem(product.Id, quantity, totalCost);
            product.IncreaseStock(quantity);
            movements.Add(new InventoryMovement(product.Id, InventoryMovementType.Entry, quantity, "Purchase registration", request.UserId, purchase.Id));
            total += totalCost.Amount;
        }

        await _purchases.AddAsync(purchase, movements, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new RegisterPurchaseResult(purchase.Id, supplier.Id, total, purchase.Items.Count);
    }
}
