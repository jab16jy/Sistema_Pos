using POS.Application.DTOs.Invoices;
using POS.Application.Interfaces;

namespace POS.Application.UseCases.Invoices;

public sealed class GetInvoiceDetailUseCase
{
    private readonly ISalesHistoryQueryService _salesHistory;

    public GetInvoiceDetailUseCase(ISalesHistoryQueryService salesHistory)
    {
        _salesHistory = salesHistory;
    }

    public Task<InvoiceDetailResult?> ExecuteAsync(Guid saleId, CancellationToken cancellationToken = default)
        => _salesHistory.GetDetailAsync(saleId, cancellationToken);
}
