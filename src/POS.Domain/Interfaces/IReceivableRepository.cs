using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface IReceivableRepository
{
    Task AddAsync(Receivable receivable, CancellationToken cancellationToken = default);
    Task<Receivable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(Receivable receivable, CancellationToken cancellationToken = default);
}
