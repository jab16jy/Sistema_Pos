using POS.Application.DTOs.Products;

namespace POS.Application.Interfaces;

public interface IProductCatalogQueryService
{
    Task<IReadOnlyList<ProductSearchResult>> SearchAsync(
        SearchProductsRequest request,
        CancellationToken cancellationToken = default);
}
