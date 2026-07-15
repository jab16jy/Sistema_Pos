using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface IReceivableRepository
{
    Task AddAsync(Receivable receivable, CancellationToken cancellationToken = default);
    Task<Receivable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Receivable>> GetOpenByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Receivable>>([]);
    Task<Receivable?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default)
        => Task.FromResult<Receivable?>(null);
    Task UpdateAsync(Receivable receivable, CancellationToken cancellationToken = default);
}
