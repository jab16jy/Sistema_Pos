using POS.Application.DTOs.Customers;

namespace POS.Application.Interfaces;

public interface ICustomerHistoryQueryService
{
    Task<CustomerHistoryResult> GetHistoryAsync(Guid customerId, CancellationToken cancellationToken = default);
}
