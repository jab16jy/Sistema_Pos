using POS.Application.DTOs.Purchases;

namespace POS.Application.Interfaces;

public interface ISupplierCatalogQueryService
{
    Task<IReadOnlyList<SupplierCatalogItemResult>> GetForSupplierAsync(Guid supplierId, bool eligibleOnly, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierCatalogItemResult>> GetForProductAsync(Guid productId, CancellationToken cancellationToken = default);
}
