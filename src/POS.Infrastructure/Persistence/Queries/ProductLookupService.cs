using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Products;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Persistence.Queries;

public sealed class ProductLookupService : IProductLookupService
{
    private readonly AppDbContext _dbContext;

    public ProductLookupService(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<IReadOnlyList<ProductLookupResult>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        string normalized = (query ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            return [];
        }

        string pattern = $"%{normalized}%";

        return await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.IsActive &&
                (EF.Functions.Like(product.Name, pattern) ||
                 (product.Barcode != null && product.Barcode == normalized)))
            .OrderBy(product => product.Name)
            .Take(20)
            .Select(product => new ProductLookupResult(
                product.Id,
                product.Name,
                product.Barcode ?? string.Empty,
                product.SalePrice.Amount,
                product.CurrentStock))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
