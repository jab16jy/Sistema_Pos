using POS.Application.DTOs.Invoices;
using POS.Application.Interfaces;

namespace POS.Application.UseCases.Invoices;

public sealed class QuerySalesHistoryUseCase
{
    private readonly ISalesHistoryQueryService _salesHistory;

    public QuerySalesHistoryUseCase(ISalesHistoryQueryService salesHistory)
    {
        _salesHistory = salesHistory;
    }

    public Task<IReadOnlyList<SaleHistoryResult>> ExecuteAsync(
        SalesHistoryRequest request,
        CancellationToken cancellationToken = default)
        => _salesHistory.SearchAsync(request, cancellationToken);
}
