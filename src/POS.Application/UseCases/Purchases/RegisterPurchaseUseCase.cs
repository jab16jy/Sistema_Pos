using POS.Application.DTOs.Purchases;
using POS.Application.Services;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Purchases;

public class RegisterPurchaseUseCase
{
    private readonly ISupplierRepository _suppliers;
    private readonly IProductRepository _products;
    private readonly IProductSupplierRepository _catalog;
    private readonly IPurchaseRepository _purchases;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CriticalCommandAuthorizer _authorizer;

    public RegisterPurchaseUseCase(ISupplierRepository suppliers, IProductRepository products, IProductSupplierRepository catalog, IPurchaseRepository purchases, IUnitOfWork unitOfWork, CriticalCommandAuthorizer authorizer)
    {
        (_suppliers, _products, _catalog, _purchases, _unitOfWork, _authorizer) = (suppliers, products, catalog, purchases, unitOfWork, authorizer);
    }

    public virtual async Task<RegisterPurchaseResult> ExecuteAsync(RegisterPurchaseRequest request, CancellationToken cancellationToken = default)
    {
        CanonicalActor actor = await _authorizer.AuthorizeAsync(PermissionKeys.Purchases.Create, cancellationToken).ConfigureAwait(false);
        if (request.Items is null || request.Items.Count == 0) throw new ArgumentException("Purchase must contain at least one item.", nameof(request));

        RegisterPurchaseResult? result = null;
        await _unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            Supplier supplier = await _suppliers.GetByIdAsync(request.SupplierId, transactionToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Supplier is not available for purchase selection.");
            if (!supplier.IsActive) throw new InvalidOperationException("Supplier is not available for purchase selection.");

            var purchase = new Purchase(supplier.Id, actor.UserId, request.EffectiveDate, request.InvoiceNumber, request.PaymentTerms, request.PaymentMethod, request.Observations, request.VatRate);
            var movements = new List<InventoryMovement>();
            foreach (RegisterPurchaseItemRequest requestedItem in request.Items)
            {
                ProductSupplier catalogItem = await _catalog.GetAsync(supplier.Id, requestedItem.ProductId, transactionToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("The selected product is not in the supplier catalog.");
                if (!catalogItem.IsEligibleForPurchase) throw new InvalidOperationException("The selected supplier catalog product is inactive or requires configuration.");

                Product product = await _products.GetByIdAsync(requestedItem.ProductId, transactionToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Product was not found for purchase registration.");
                if (!product.TracksInventory) throw new InvalidOperationException("Only inventory-tracked products can be received in a purchase.");

                string reference = catalogItem.SupplierSku ?? product.SourceCode;
                string purchaseUnit = catalogItem.PurchaseUnitName!;
                decimal factor = catalogItem.UnitsPerPurchaseUnit!.Value;
                decimal purchaseUnitCost = requestedItem.PurchaseUnitCostOverride ?? catalogItem.PurchaseUnitCost!.Value;
                if (purchaseUnitCost <= 0m) throw new ArgumentOutOfRangeException(nameof(request), "Purchase-unit cost must be greater than zero.");

                var quantity = new Quantity(requestedItem.Quantity);
                var item = new PurchaseItem(product.Id, product.Name, reference, purchaseUnit, factor, quantity, new Money(purchaseUnitCost));
                purchase.AddItem(item);

                var received = new Quantity(item.InventoryUnitsReceived!.Value);
                decimal priorStock = product.CurrentStock;
                product.ApplyPurchaseReceiptCost(priorStock, received, item.BaseUnitCost!.Value);
                product.IncreaseStock(received);
                movements.Add(new InventoryMovement(product.Id, InventoryMovementType.Entry, received, "Supplier purchase receipt", actor.UserId, purchase.Id));
            }

            await _purchases.AddAsync(purchase, movements, transactionToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(transactionToken).ConfigureAwait(false);
            result = new RegisterPurchaseResult(purchase.Id, supplier.Id, purchase.Total!.Value.Amount, purchase.Items.Count, purchase.VatAmount!.Value.Amount, purchase.InvoiceNumber);
        }, cancellationToken).ConfigureAwait(false);

        return result!;
    }
}
