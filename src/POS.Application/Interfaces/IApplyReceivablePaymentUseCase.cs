using POS.Domain.Enums;

namespace POS.Application.Interfaces;

/// <summary>
/// Contract for applying a partial or full payment against an open receivable.
/// Spec: reject overpayment (payment > outstanding balance).
/// </summary>
public interface IApplyReceivablePaymentUseCase
{
    Task ExecuteAsync(Guid receivableId, decimal paymentAmount, Guid? cashierUserId = null, Guid? shiftId = null, CancellationToken cancellationToken = default);

    Task ExecuteAsync(
        Guid receivableId,
        decimal paymentAmount,
        PaymentMethod paymentMethod,
        string? bankOrPlatform,
        Guid? cashierUserId = null,
        Guid? shiftId = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(receivableId, paymentAmount, cashierUserId, shiftId, cancellationToken);

    Task<decimal> ExecuteAllAsync(
        Guid customerId,
        PaymentMethod paymentMethod,
        string? bankOrPlatform,
        Guid? cashierUserId = null,
        Guid? shiftId = null,
        CancellationToken cancellationToken = default);
}
