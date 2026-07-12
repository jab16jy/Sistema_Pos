using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Persistence.Queries;

public sealed class SupplierCatalogQueryService : ISupplierCatalogQueryService
{
    private readonly AppDbContext _dbContext;
    public SupplierCatalogQueryService(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<SupplierCatalogItemResult>> GetForSupplierAsync(Guid supplierId, bool eligibleOnly, CancellationToken cancellationToken = default)
        => await BuildQuery(item => item.SupplierId == supplierId, eligibleOnly).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<SupplierCatalogItemResult>> GetForProductAsync(Guid productId, CancellationToken cancellationToken = default)
        => await BuildQuery(item => item.ProductId == productId, eligibleOnly: false).ToListAsync(cancellationToken).ConfigureAwait(false);

    // Filters run on the entities (ProductSuppliers/Products/Suppliers) BEFORE projecting into
    // SupplierCatalogItemResult. EF Core cannot translate a Where() applied over a record projection,
    // which is what threw "The LINQ expression could not be translated" when reloading a catalog.
    private IQueryable<SupplierCatalogItemResult> BuildQuery(
        System.Linq.Expressions.Expression<Func<Domain.Entities.ProductSupplier, bool>> predicate,
        bool eligibleOnly)
    {
        var joined =
            from item in _dbContext.ProductSuppliers.AsNoTracking().Where(predicate)
            join product in _dbContext.Products.AsNoTracking() on item.ProductId equals product.Id
            join supplier in _dbContext.Suppliers.AsNoTracking() on item.SupplierId equals supplier.Id
            select new { item, product, supplier };

        if (eligibleOnly)
            joined = joined.Where(x =>
                x.item.IsActive && x.supplier.IsActive && x.product.IsActive &&
                x.item.PurchaseUnitName != null && x.item.UnitsPerPurchaseUnit > 0m && x.item.PurchaseUnitCost > 0m);

        return joined
            .OrderBy(x => x.product.Name)
            .Select(x => new SupplierCatalogItemResult(
                x.item.SupplierId, x.supplier.Name, x.item.ProductId, x.product.Name, x.product.SourceCode,
                x.item.SupplierSku, x.item.PurchaseUnitName, x.item.UnitsPerPurchaseUnit, x.item.PurchaseUnitCost,
                x.item.IsActive && x.supplier.IsActive && x.product.IsActive));
    }
}
