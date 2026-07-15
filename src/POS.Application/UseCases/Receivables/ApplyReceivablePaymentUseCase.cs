using POS.Application.Interfaces;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Receivables;

/// <summary>
/// Applies a partial or full payment toward an open receivable balance.
/// Spec: reject overpayment (payment > outstanding balance).
/// Spec: reject payment of zero or negative amounts.
/// </summary>
public sealed class ApplyReceivablePaymentUseCase : IApplyReceivablePaymentUseCase
{
    private readonly IReceivableRepository _receivables;
    private readonly IUnitOfWork _unitOfWork;

    public ApplyReceivablePaymentUseCase(IReceivableRepository receivables, IUnitOfWork unitOfWork)
    {
        _receivables = receivables;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(Guid receivableId, decimal paymentAmount, CancellationToken cancellationToken = default)
        => await ExecuteAsync(receivableId, paymentAmount, PaymentMethod.Cash, null, cancellationToken).ConfigureAwait(false);

    public async Task ExecuteAsync(
        Guid receivableId,
        decimal paymentAmount,
        PaymentMethod paymentMethod,
        string? bankOrPlatform,
        CancellationToken cancellationToken = default)
    {
        if (paymentAmount <= 0m)
        {
            throw new InvalidOperationException("Payment amount must be greater than zero.");
        }

        var receivable = await _receivables.GetByIdAsync(receivableId, cancellationToken)
            ?? throw new InvalidOperationException($"Receivable '{receivableId}' was not found.");

        // Overpayment guard — Receivable.ApplyPayment also throws, but we check here for a clear message
        if (paymentAmount > receivable.OutstandingBalance.Amount)
        {
            throw new InvalidOperationException(
                $"Payment amount ({paymentAmount:N0}) exceeds outstanding balance ({receivable.OutstandingBalance.Amount:N0}).");
        }

        PaymentPlatforms.Validate(paymentMethod, bankOrPlatform);
        receivable.ApplyPayment(new Money(paymentAmount), paymentMethod, bankOrPlatform);

        await _receivables.UpdateAsync(receivable, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<decimal> ExecuteAllAsync(
        Guid customerId,
        PaymentMethod paymentMethod,
        string? bankOrPlatform,
        CancellationToken cancellationToken = default)
    {
        if (customerId == Guid.Empty)
        {
            throw new InvalidOperationException("Customer id is required.");
        }

        PaymentPlatforms.Validate(paymentMethod, bankOrPlatform);

        decimal amountApplied = 0m;
        await _unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            IReadOnlyList<Receivable> receivables = await _receivables
                .GetOpenByCustomerIdAsync(customerId, transactionCancellationToken)
                .ConfigureAwait(false);

            amountApplied = receivables.Sum(receivable => receivable.OutstandingBalance.Amount);
            if (amountApplied <= 0m)
            {
                throw new InvalidOperationException("The customer has no outstanding balance.");
            }

            foreach (Receivable receivable in receivables)
            {
                receivable.ApplyPayment(
                    receivable.OutstandingBalance,
                    paymentMethod,
                    bankOrPlatform);
                await _receivables.UpdateAsync(receivable, transactionCancellationToken).ConfigureAwait(false);
            }

            await _unitOfWork.SaveChangesAsync(transactionCancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return amountApplied;
    }
}
