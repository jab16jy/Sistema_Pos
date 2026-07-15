using POS.Application.DTOs.Sales;
using POS.Application.Common;
using POS.Application.DTOs.Loyalty;
using POS.Application.UseCases.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Sales;

public sealed class CompleteHeldSaleUseCase
{
    private readonly ISaleRepository _sales;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AccumulateLoyaltyPointsUseCase? _accumulatePoints;

    public CompleteHeldSaleUseCase(
        ISaleRepository sales,
        IUnitOfWork unitOfWork,
        AccumulateLoyaltyPointsUseCase? accumulatePoints = null)
    {
        _sales = sales;
        _unitOfWork = unitOfWork;
        _accumulatePoints = accumulatePoints;
    }

    public async Task<CreateSaleResult> ExecuteAsync(
        CompleteHeldSaleRequest request,
        CancellationToken cancellationToken = default)
    {
        Sale sale = await _sales.GetByIdAsync(request.SaleId, cancellationToken)
            ?? throw new InvalidOperationException("The held sale was not found.");

        if (sale.CashierUserId != request.CashierUserId)
        {
            throw new UnauthorizedAccessException("The held sale belongs to another cashier.");
        }

        if (sale.Status != SaleStatus.Draft)
        {
            throw new InvalidOperationException("Resume the held sale before completing it.");
        }

        foreach (CreateSalePaymentRequest payment in request.Payments)
        {
            if (!string.IsNullOrWhiteSpace(payment.BankOrPlatform)
                && !PaymentPlatforms.IsValid(payment.BankOrPlatform))
            {
                throw new InvalidOperationException(
                    $"Payment platform '{payment.BankOrPlatform}' is not accepted. " +
                    $"Valid platforms: {string.Join(", ", PaymentPlatforms.All)}.");
            }

            sale.AddPayment(new Payment(
                payment.Method,
                new Money(payment.Amount),
                payment.ReceivedAmount is null ? null : new Money(payment.ReceivedAmount.Value),
                payment.BankOrPlatform));
        }

        sale.Complete();

        if (_accumulatePoints is not null && sale.CustomerId.HasValue)
        {
            await _accumulatePoints.RecordForSaleAsync(
                new EarnPointsRequest(sale.CustomerId.Value, sale.Id, sale.EffectiveEarnableTotal().Amount),
                cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CreateSaleResult(
            sale.Id,
            sale.InvoiceNumber,
            sale.Total().Amount,
            sale.CashInDrawer().Amount,
            sale.DeferredBalance().Amount);
    }
}
