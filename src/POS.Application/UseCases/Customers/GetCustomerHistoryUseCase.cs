using POS.Application.DTOs.Customers;
using POS.Application.Interfaces;

namespace POS.Application.UseCases.Customers;

public sealed class GetCustomerHistoryUseCase
{
    private readonly ICustomerHistoryQueryService _queryService;

    public GetCustomerHistoryUseCase(ICustomerHistoryQueryService queryService)
    {
        _queryService = queryService;
    }

    public Task<CustomerHistoryResult> ExecuteAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("Customer id is required.", nameof(customerId));
        }

        return _queryService.GetHistoryAsync(customerId, cancellationToken);
    }
}
