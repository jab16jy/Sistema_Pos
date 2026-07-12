using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Persistence.Queries;

public sealed class SupplierCatalogQueryService : ISupplierCatalogQueryService
{
    private readonly AppDbContext _dbContext;
    public SupplierCatalogQueryService(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<SupplierCatalogItemResult>> GetActiveAsync(Guid supplierId, CancellationToken cancellationToken = default)
        => await (from item in _dbContext.ProductSuppliers.AsNoTracking()
                  join product in _dbContext.Products.AsNoTracking() on item.ProductId equals product.Id
                  join supplier in _dbContext.Suppliers.AsNoTracking() on item.SupplierId equals supplier.Id
                  where item.SupplierId == supplierId && item.IsActive && product.IsActive && supplier.IsActive
                  orderby product.Name
                  select new SupplierCatalogItemResult(item.SupplierId, item.ProductId, product.Name, product.SourceCode, item.SupplierSku, item.PurchaseUnitName, item.UnitsPerPurchaseUnit, item.PurchaseUnitCost, item.IsActive))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
}
