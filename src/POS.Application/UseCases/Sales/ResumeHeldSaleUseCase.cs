using POS.Application.DTOs.Sales;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Sales;

public sealed class ResumeHeldSaleUseCase
{
    private readonly ISaleRepository _sales;
    private readonly IUnitOfWork _unitOfWork;

    public ResumeHeldSaleUseCase(ISaleRepository sales, IUnitOfWork unitOfWork)
    {
        _sales = sales;
        _unitOfWork = unitOfWork;
    }

    public async Task<HeldSaleSummary> ExecuteAsync(
        ResumeHeldSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        Sale sale = await _sales.GetByIdAsync(request.SaleId, cancellationToken)
            ?? throw new InvalidOperationException("The held sale was not found.");

        if (sale.CashierUserId != request.CashierUserId)
        {
            throw new UnauthorizedAccessException("The held sale belongs to another cashier.");
        }

        if (sale.Status != SaleStatus.OnHold)
        {
            throw new InvalidOperationException("Only held sales can be resumed.");
        }

        sale.Resume();
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return HeldSaleMapper.Map(sale);
    }
}
