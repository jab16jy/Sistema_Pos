using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface ISaleReturnRepository
{
    Task AddAsync(SaleReturn saleReturn, CancellationToken cancellationToken = default);
    Task<SaleReturn?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SaleReturn>> GetAllBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default);
}
