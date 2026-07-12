using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class ProductSupplierRepository : IProductSupplierRepository
{
    private readonly AppDbContext _dbContext;
    public ProductSupplierRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public Task<ProductSupplier?> GetAsync(Guid supplierId, Guid productId, CancellationToken cancellationToken = default)
        => _dbContext.ProductSuppliers.FirstOrDefaultAsync(item => item.SupplierId == supplierId && item.ProductId == productId, cancellationToken);

    public async Task<IReadOnlyList<ProductSupplier>> GetActiveForSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default)
        => await _dbContext.ProductSuppliers.Where(item => item.SupplierId == supplierId && item.IsActive).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddAsync(ProductSupplier catalogItem, CancellationToken cancellationToken = default)
    {
        await _dbContext.ProductSuppliers.AddAsync(catalogItem, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(ProductSupplier catalogItem, CancellationToken cancellationToken = default)
    {
        _dbContext.ProductSuppliers.Update(catalogItem);
        return Task.CompletedTask;
    }
}
