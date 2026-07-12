using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;

namespace POS.Application.UseCases.Purchases;

public sealed class SearchPurchaseHistoryUseCase
{
    private readonly IPurchaseHistoryQueryService _history;
    public SearchPurchaseHistoryUseCase(IPurchaseHistoryQueryService history) => _history = history;

    public Task<PagedResult<PurchaseHistoryResult>> ExecuteAsync(PurchaseHistoryRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Page < 1 || request.PageSize is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(request));
        if (request.FromEffectiveDate > request.ToEffectiveDate) throw new ArgumentException("The start date cannot be after the end date.", nameof(request));
        return _history.SearchAsync(request, cancellationToken);
    }
}
