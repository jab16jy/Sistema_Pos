using POS.Application.DTOs.Purchases;

namespace POS.Application.Interfaces;

public interface ISupplierLookupQueryService
{
    Task<IReadOnlyList<SupplierSearchResult>> SearchAsync(SupplierSearchRequest request, CancellationToken cancellationToken = default);
}
