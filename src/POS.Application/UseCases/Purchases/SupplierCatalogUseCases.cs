using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Purchases;

public sealed class GetSupplierCatalogUseCase
{
    private readonly ISupplierCatalogQueryService _catalog;
    public GetSupplierCatalogUseCase(ISupplierCatalogQueryService catalog) => _catalog = catalog;
    public Task<IReadOnlyList<SupplierCatalogItemResult>> ExecuteAsync(Guid supplierId, CancellationToken cancellationToken = default)
        => supplierId == Guid.Empty ? throw new ArgumentException("Supplier is required.", nameof(supplierId)) : _catalog.GetForSupplierAsync(supplierId, false, cancellationToken);
}

public sealed class GetEligibleSupplierCatalogUseCase
{
    private readonly ISupplierCatalogQueryService _catalog;
    public GetEligibleSupplierCatalogUseCase(ISupplierCatalogQueryService catalog) => _catalog = catalog;
    public Task<IReadOnlyList<SupplierCatalogItemResult>> ExecuteAsync(Guid supplierId, CancellationToken cancellationToken = default)
        => supplierId == Guid.Empty ? throw new ArgumentException("Supplier is required.", nameof(supplierId)) : _catalog.GetForSupplierAsync(supplierId, true, cancellationToken);
}

public sealed class GetProductSupplierSummaryUseCase
{
    private readonly ISupplierCatalogQueryService _catalog;
    public GetProductSupplierSummaryUseCase(ISupplierCatalogQueryService catalog) => _catalog = catalog;
    public Task<IReadOnlyList<SupplierCatalogItemResult>> ExecuteAsync(Guid productId, CancellationToken cancellationToken = default)
        => productId == Guid.Empty ? throw new ArgumentException("Product is required.", nameof(productId)) : _catalog.GetForProductAsync(productId, cancellationToken);
}

public sealed class SaveSupplierCatalogItemUseCase
{
    private readonly ISupplierRepository _suppliers;
    private readonly IProductRepository _products;
    private readonly IProductSupplierRepository _catalog;
    private readonly IUnitOfWork _unitOfWork;

    public SaveSupplierCatalogItemUseCase(ISupplierRepository suppliers, IProductRepository products, IProductSupplierRepository catalog, IUnitOfWork unitOfWork)
        => (_suppliers, _products, _catalog, _unitOfWork) = (suppliers, products, catalog, unitOfWork);

    public async Task ExecuteAsync(SaveSupplierCatalogItemRequest request, CancellationToken cancellationToken = default)
    {
        Supplier supplier = await _suppliers.GetByIdAsync(request.SupplierId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Supplier was not found.");
        if (!supplier.IsActive) throw new InvalidOperationException("Inactive suppliers cannot maintain a purchase catalog.");
        _ = await _products.GetByIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Product was not found.");

        ProductSupplier? item = await _catalog.GetAsync(request.SupplierId, request.ProductId, cancellationToken).ConfigureAwait(false);
        if (item is null)
            await _catalog.AddAsync(new ProductSupplier(request.ProductId, request.SupplierId, request.SupplierSku, request.PurchaseUnitName, request.UnitsPerPurchaseUnit, request.PurchaseUnitCost, request.IsActive), cancellationToken).ConfigureAwait(false);
        else
        {
            item.Update(request.SupplierSku, request.PurchaseUnitName, request.UnitsPerPurchaseUnit, request.PurchaseUnitCost, request.IsActive);
            await _catalog.UpdateAsync(item, cancellationToken).ConfigureAwait(false);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
