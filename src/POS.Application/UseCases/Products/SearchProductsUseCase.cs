using POS.Application.DTOs.Products;
using POS.Application.Interfaces;

namespace POS.Application.UseCases.Products;

public sealed class SearchProductsUseCase
{
    private readonly IProductCatalogQueryService _queryService;

    public SearchProductsUseCase(IProductCatalogQueryService queryService)
    {
        _queryService = queryService;
    }

    public Task<IReadOnlyList<ProductSearchResult>> ExecuteAsync(
        SearchProductsRequest request,
        CancellationToken cancellationToken = default)
        => _queryService.SearchAsync(request, cancellationToken);
}
