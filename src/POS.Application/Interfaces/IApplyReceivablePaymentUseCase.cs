namespace POS.Application.Interfaces;

/// <summary>
/// Contract for applying a partial or full payment against an open receivable.
/// Spec: reject overpayment (payment > outstanding balance).
/// </summary>
public interface IApplyReceivablePaymentUseCase
{
    Task ExecuteAsync(Guid receivableId, decimal paymentAmount, CancellationToken cancellationToken = default);
}
