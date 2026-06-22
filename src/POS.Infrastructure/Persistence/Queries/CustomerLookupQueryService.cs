using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Customers;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Persistence.Queries;

public sealed class CustomerLookupQueryService : ICustomerLookupQueryService
{
    private readonly AppDbContext _dbContext;

    public CustomerLookupQueryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CustomerSearchResult>> SearchAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default)
    {
        IQueryable<Domain.Entities.Customer> query = _dbContext.Customers.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(customer => customer.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            string normalized = request.SearchText.Trim();
            query = query.Where(customer =>
                customer.FullName.Contains(normalized) ||
                customer.Identification.Contains(normalized) ||
                (customer.Phone != null && customer.Phone.Contains(normalized)) ||
                (customer.Email != null && customer.Email.Contains(normalized)) ||
                (customer.Address != null && customer.Address.Contains(normalized)));
        }

        return await query
            .OrderByDescending(customer => customer.IsActive)
            .ThenBy(customer => customer.FullName)
            .Take(50)
            .Select(customer => new CustomerSearchResult(
                customer.Id,
                customer.FullName,
                customer.Identification,
                customer.Phone,
                customer.Email,
                customer.Address,
                customer.IsActive))
            .ToListAsync(cancellationToken);
    }
}
