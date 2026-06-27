using POS.Application.DTOs.Invoices;

namespace POS.Application.Interfaces;

public interface ISalesHistoryQueryService
{
    Task<IReadOnlyList<SaleHistoryResult>> SearchAsync(
        SalesHistoryRequest request,
        CancellationToken cancellationToken = default);
}
