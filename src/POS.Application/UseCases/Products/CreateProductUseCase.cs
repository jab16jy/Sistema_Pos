using POS.Application.DTOs.Products;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Products;

public sealed class CreateProductUseCase
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IInventoryRepository? _inventory;

    public CreateProductUseCase(
        IProductRepository products,
        ICategoryRepository categories,
        IUnitOfWork unitOfWork,
        IInventoryRepository? inventory = null)
    {
        _products = products;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _inventory = inventory;
    }

    public async Task<Guid> ExecuteAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(request.Barcode) &&
            await _products.GetByBarcodeAsync(request.Barcode, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException("Another product already uses this barcode.");
        }

        // Products never write CurrentStock directly: the product starts at zero and any initial
        // balance is applied as an audited InventoryMovement so saldo e historial always reconcile.
        // ponytail: the null ledger path exists only for unit fixtures; production DI always injects it.
        bool ledgerAvailable = _inventory is not null;
        decimal seedStock = ledgerAvailable ? 0m : request.InitialStock;

        var product = new Product(
            request.Name,
            request.CategoryId,
            request.CostPrice.HasValue ? new Money(request.CostPrice.Value) : null,
            new Money(request.SalePrice),
            new StockQuantity(seedStock),
            new StockQuantity(request.MinimumStock),
            request.Description,
            string.IsNullOrWhiteSpace(request.Barcode) ? null : new Barcode(request.Barcode),
            request.TaxRate,
            request.UnitsPerBox,
            request.BoxPrice.HasValue ? new Money(request.BoxPrice.Value) : null,
            request.HalfBoxUnits,
            request.HalfBoxPrice.HasValue ? new Money(request.HalfBoxPrice.Value) : null,
            request.QuarterBoxUnits,
            request.QuarterBoxPrice.HasValue ? new Money(request.QuarterBoxPrice.Value) : null);

        await _products.AddAsync(product, cancellationToken);

        if (ledgerAvailable && request.InitialStock > 0m)
        {
            InventoryMovement movement = product.RegisterStockMovement(
                InventoryMovementType.InitialBalance,
                request.InitialStock,
                "Saldo inicial",
                request.OperatorUserId);
            await _inventory!.AddMovementAsync(movement, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return product.Id;
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category is required.", nameof(categoryId));
        }

        if (await _categories.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new InvalidOperationException("Selected category was not found.");
        }
    }
}
