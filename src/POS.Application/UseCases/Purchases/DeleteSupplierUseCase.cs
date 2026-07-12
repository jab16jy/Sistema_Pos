using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Purchases;

/// <summary>
/// Deletes a supplier and its purchase catalog. A supplier with registered purchases cannot be
/// deleted (it would break the purchase history) — the caller should deactivate it instead.
/// Mirrors the DeleteCategoryUseCase guard-then-delete convention.
/// </summary>
public sealed class DeleteSupplierUseCase
{
    private readonly ISupplierRepository _suppliers;
    private readonly IProductSupplierRepository _catalog;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSupplierUseCase(ISupplierRepository suppliers, IProductSupplierRepository catalog, IUnitOfWork unitOfWork)
        => (_suppliers, _catalog, _unitOfWork) = (suppliers, catalog, unitOfWork);

    public async Task ExecuteAsync(Guid supplierId, CancellationToken cancellationToken = default)
    {
        if (supplierId == Guid.Empty) throw new ArgumentException("Supplier is required.", nameof(supplierId));

        _ = await _suppliers.GetByIdAsync(supplierId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Supplier was not found.");

        if (await _suppliers.HasPurchasesAsync(supplierId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("No se puede eliminar un proveedor con compras registradas. Desactívalo en su lugar.");
        }

        // Remove the catalog relations first so no orphan ProductSupplier rows remain.
        IReadOnlyList<ProductSupplier> catalogItems = await _catalog.GetForSupplierAsync(supplierId, cancellationToken).ConfigureAwait(false);
        foreach (ProductSupplier item in catalogItems)
        {
            await _catalog.RemoveAsync(item, cancellationToken).ConfigureAwait(false);
        }

        await _suppliers.DeleteAsync(supplierId, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
