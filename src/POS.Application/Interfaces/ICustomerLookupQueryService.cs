using POS.Application.DTOs.Customers;

namespace POS.Application.Interfaces;

public interface ICustomerLookupQueryService
{
    Task<IReadOnlyList<CustomerSearchResult>> SearchAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default);
}
