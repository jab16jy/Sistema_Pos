using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly AppDbContext _dbContext;

    public SupplierRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Suppliers.FirstOrDefaultAsync(supplier => supplier.Id == id, cancellationToken);

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        await _dbContext.Suppliers.AddAsync(supplier, cancellationToken);
    }

    public Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        _dbContext.Suppliers.Update(supplier);
        return Task.CompletedTask;
    }

    public Task<bool> HasPurchasesAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Purchases.AsNoTracking().AnyAsync(purchase => purchase.SupplierId == id, cancellationToken);

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Supplier? supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(supplier => supplier.Id == id, cancellationToken).ConfigureAwait(false);
        if (supplier is not null)
        {
            _dbContext.Suppliers.Remove(supplier);
        }
    }
}
