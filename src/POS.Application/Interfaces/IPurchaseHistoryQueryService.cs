using POS.Application.DTOs.Purchases;

namespace POS.Application.Interfaces;

public interface IPurchaseHistoryQueryService
{
    Task<PagedResult<PurchaseHistoryResult>> SearchAsync(PurchaseHistoryRequest request, CancellationToken cancellationToken = default);
}
