using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Customer?> GetByIdentificationAsync(string identification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Customer>> SearchAsync(string? searchText, bool activeOnly, CancellationToken cancellationToken = default);

    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);

    Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
}
