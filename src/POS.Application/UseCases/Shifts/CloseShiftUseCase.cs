using POS.Application.DTOs.Shifts;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Shifts;

public sealed class CloseShiftUseCase
{
    private readonly ICashierShiftRepository _shifts;
    private readonly ISaleRepository _sales;
    private readonly IUnitOfWork _unitOfWork;

    public CloseShiftUseCase(ICashierShiftRepository shifts, ISaleRepository sales, IUnitOfWork unitOfWork)
    {
        _shifts = shifts;
        _sales = sales ?? throw new ArgumentNullException(nameof(sales));
        _unitOfWork = unitOfWork;
    }

    public async Task<ShiftSummary> ExecuteAsync(CloseShiftRequest request, CancellationToken ct = default)
    {
        var shift = await _shifts.GetByIdAsync(request.ShiftId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Turno no encontrado.");

        DateTimeOffset closedAt = DateTimeOffset.UtcNow;
        ShiftSalesSummaryData salesSummary = await _sales
            .GetShiftSalesSummaryAsync(shift.CashierUserId, shift.OpenedAt, closedAt, ct)
            .ConfigureAwait(false);

        shift.Close(
            new Money(request.ClosingCash),
            new ShiftClosingSummary(
                new Money(salesSummary.SalesTotal),
                new Money(salesSummary.CashPayments),
                new Money(salesSummary.DigitalPayments)),
            closedAt,
            request.Notes);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        return StartShiftUseCase.ToSummary(shift);
    }
}
