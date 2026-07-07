using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly AppDbContext _dbContext;

    public CustomerRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Customers.FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

    public Task<Customer?> GetByIdentificationAsync(string identification, CancellationToken cancellationToken = default)
        => _dbContext.Customers.FirstOrDefaultAsync(customer => customer.Identification == identification, cancellationToken);

    public async Task<IReadOnlyList<Customer>> SearchAsync(string? searchText, bool activeOnly, CancellationToken cancellationToken = default)
    {
        IQueryable<Customer> query = _dbContext.Customers.AsNoTracking();

        if (activeOnly)
        {
            query = query.Where(customer => customer.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string normalized = searchText.Trim();
            query = query.Where(customer =>
                customer.FullName.Contains(normalized) ||
                customer.Identification.Contains(normalized) ||
                (customer.Phone != null && customer.Phone.Contains(normalized)) ||
                (customer.Email != null && customer.Email.Contains(normalized)));
        }

        return await query
            .OrderBy(customer => customer.FullName)
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await _dbContext.Customers.AddAsync(customer, cancellationToken);
    }

    public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        _dbContext.Customers.Update(customer);
        return Task.CompletedTask;
    }
}
