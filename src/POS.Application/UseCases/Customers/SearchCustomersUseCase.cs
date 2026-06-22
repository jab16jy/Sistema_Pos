using POS.Application.DTOs.Customers;
using POS.Application.Interfaces;

namespace POS.Application.UseCases.Customers;

public sealed class SearchCustomersUseCase
{
    private readonly ICustomerLookupQueryService _queryService;

    public SearchCustomersUseCase(ICustomerLookupQueryService queryService)
    {
        _queryService = queryService;
    }

    public Task<IReadOnlyList<CustomerSearchResult>> ExecuteAsync(CustomerSearchRequest request, CancellationToken cancellationToken = default)
        => _queryService.SearchAsync(request, cancellationToken);
}
