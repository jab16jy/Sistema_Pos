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
    private readonly IReceivableRepository? _receivables;
    private readonly ISettingsRepository? _settings;

    public CompleteHeldSaleUseCase(
        ISaleRepository sales,
        IUnitOfWork unitOfWork,
        AccumulateLoyaltyPointsUseCase? accumulatePoints = null,
        IReceivableRepository? receivables = null,
        ISettingsRepository? settings = null)
    {
        _sales = sales;
        _unitOfWork = unitOfWork;
        _accumulatePoints = accumulatePoints;
        _receivables = receivables;
        _settings = settings;
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

        if (request.SettlementPlan?.DeferredBalanceAmount > 0m && _receivables is null)
        {
            throw new InvalidOperationException("Receivable persistence is required for deferred sales.");
        }

        if (request.SettlementPlan is not null)
        {
            SaleSettlementPlan plan = request.SettlementPlan;
            if (plan.DeferredBalanceAmount > 0m && sale.CustomerId is null)
            {
                throw new InvalidOperationException("A customer must be identified to create a deferred balance.");
            }

            if (plan.CollectedMethod is { } method)
            {
                PaymentPlatforms.Validate(method, plan.CollectedBankOrPlatform);
            }

            decimal maximumDeferredPercent = await DeferredBalancePolicy
                .GetMaximumAsync(_settings, cancellationToken)
                .ConfigureAwait(false);
            DeferredBalancePolicy.Validate(plan.DeferredBalanceAmount, sale.Total().Amount, maximumDeferredPercent);

            sale.SetSettlementPlan(plan);
            if (plan.CollectedAmount > 0m && plan.CollectedMethod is { } collectedMethod)
            {
                sale.AddPayment(new Payment(
                    collectedMethod,
                    new Money(plan.CollectedAmount),
                    collectedMethod == PaymentMethod.Cash ? new Money(plan.CollectedAmount) : null,
                    plan.CollectedBankOrPlatform));
            }
        }
        else
        {
            foreach (CreateSalePaymentRequest payment in request.Payments)
            {
                PaymentPlatforms.Validate(payment.Method, payment.BankOrPlatform);

                sale.AddPayment(new Payment(
                    payment.Method,
                    new Money(payment.Amount),
                    payment.ReceivedAmount is null ? null : new Money(payment.ReceivedAmount.Value),
                    payment.BankOrPlatform));
            }
        }

        sale.Complete();

        if (sale.DeferredBalance().Amount > 0m)
        {
            await (_receivables ?? throw new InvalidOperationException("Receivable persistence is required for deferred sales."))
                .AddAsync(
                new Receivable(sale.CustomerId!.Value, sale.Id, sale.DeferredBalance()),
                cancellationToken).ConfigureAwait(false);
        }

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
