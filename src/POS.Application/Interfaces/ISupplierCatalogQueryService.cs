using POS.Application.DTOs.Purchases;

namespace POS.Application.Interfaces;

public interface ISupplierCatalogQueryService
{
    Task<IReadOnlyList<SupplierCatalogItemResult>> GetActiveAsync(Guid supplierId, CancellationToken cancellationToken = default);
}
