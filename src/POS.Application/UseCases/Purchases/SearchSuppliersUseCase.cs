using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;

namespace POS.Application.UseCases.Purchases;

public sealed class SearchSuppliersUseCase
{
    private readonly ISupplierLookupQueryService _supplierLookup;

    public SearchSuppliersUseCase(ISupplierLookupQueryService supplierLookup)
    {
        _supplierLookup = supplierLookup;
    }

    public Task<IReadOnlyList<SupplierSearchResult>> ExecuteAsync(SupplierSearchRequest request, CancellationToken cancellationToken = default)
        => _supplierLookup.SearchAsync(request, cancellationToken);
}
