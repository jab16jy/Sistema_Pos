using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Products;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Persistence.Queries;

public sealed class ProductCatalogQueryService : IProductCatalogQueryService
{
    private readonly AppDbContext _dbContext;

    public ProductCatalogQueryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProductSearchResult>> SearchAsync(
        SearchProductsRequest request,
        CancellationToken cancellationToken = default)
    {
        string? searchText = string.IsNullOrWhiteSpace(request.SearchText)
            ? null
            : request.SearchText.Trim();

        IQueryable<Domain.Entities.Product> query = _dbContext.Products.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(product => product.IsActive);
        }

        if (request.CategoryId.HasValue)
        {
            query = query.Where(product => product.CategoryId == request.CategoryId.Value);
        }

        if (searchText is not null)
        {
            string pattern = $"%{searchText}%";
            query = query.Where(product =>
                EF.Functions.Like(product.Name, pattern) ||
                (product.Barcode != null && product.Barcode == searchText));
        }

        return await query
            .OrderBy(product => product.Name)
            .Select(product => new ProductSearchResult(
                product.Id,
                product.Name,
                product.CategoryId,
                product.SalePrice.Amount,
                product.CurrentStock,
                product.MinimumStock,
                product.Description,
                product.Barcode,
                product.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
