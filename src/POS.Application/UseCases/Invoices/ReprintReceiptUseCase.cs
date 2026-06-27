using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Invoices;

public sealed class ReprintReceiptUseCase
{
    private readonly ISaleRepository _sales;
    private readonly IPrintService _printService;

    public ReprintReceiptUseCase(ISaleRepository sales, IPrintService printService)
    {
        _sales = sales;
        _printService = printService;
    }

    public async Task ExecuteAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        Sale sale = await _sales.GetByIdAsync(saleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Sale was not found.");

        if (sale.Status == SaleStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot reprint: sale was cancelled");
        }

        if (sale.Status != SaleStatus.Completed)
        {
            throw new InvalidOperationException("Only completed sales can be reprinted.");
        }

        await _printService.PrintReceiptAsync(sale.Id, cancellationToken).ConfigureAwait(false);
    }
}
