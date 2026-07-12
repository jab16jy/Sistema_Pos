using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface IProductSupplierRepository
{
    Task<ProductSupplier?> GetAsync(Guid supplierId, Guid productId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductSupplier>> GetForSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductSupplier>> GetActiveForSupplierAsync(Guid supplierId, CancellationToken cancellationToken = default);
    Task AddAsync(ProductSupplier catalogItem, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProductSupplier catalogItem, CancellationToken cancellationToken = default);
    Task RemoveAsync(ProductSupplier catalogItem, CancellationToken cancellationToken = default);
}
