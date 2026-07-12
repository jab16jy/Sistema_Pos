using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Persistence.Queries;

public sealed class SupplierCatalogQueryService : ISupplierCatalogQueryService
{
    private readonly AppDbContext _dbContext;
    public SupplierCatalogQueryService(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<SupplierCatalogItemResult>> GetForSupplierAsync(Guid supplierId, bool eligibleOnly, CancellationToken cancellationToken = default)
    {
        IQueryable<SupplierCatalogItemResult> query = BuildQuery().Where(item => item.SupplierId == supplierId);
        if (eligibleOnly)
            query = query.Where(item => item.IsActive && item.PurchaseUnitName != null && item.UnitsPerPurchaseUnit > 0m && item.PurchaseUnitCost > 0m);
        return await query.OrderBy(item => item.ProductName).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SupplierCatalogItemResult>> GetForProductAsync(Guid productId, CancellationToken cancellationToken = default)
        => await BuildQuery().Where(item => item.ProductId == productId).OrderBy(item => item.ProductName).ToListAsync(cancellationToken).ConfigureAwait(false);

    private IQueryable<SupplierCatalogItemResult> BuildQuery() =>
        from item in _dbContext.ProductSuppliers.AsNoTracking()
        join product in _dbContext.Products.AsNoTracking() on item.ProductId equals product.Id
        join supplier in _dbContext.Suppliers.AsNoTracking() on item.SupplierId equals supplier.Id
        select new SupplierCatalogItemResult(item.SupplierId, supplier.Name, item.ProductId, product.Name, product.SourceCode, item.SupplierSku, item.PurchaseUnitName, item.UnitsPerPurchaseUnit, item.PurchaseUnitCost, item.IsActive && supplier.IsActive && product.IsActive);
}
