using POS.Application.DTOs.Shifts;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Shifts;

public sealed class StartShiftUseCase
{
    private readonly ICashierShiftRepository _shifts;
    private readonly IUnitOfWork _unitOfWork;

    public StartShiftUseCase(ICashierShiftRepository shifts, IUnitOfWork unitOfWork)
    {
        _shifts = shifts;
        _unitOfWork = unitOfWork;
    }

    public async Task<ShiftSummary> ExecuteAsync(StartShiftRequest request, CancellationToken ct = default)
    {
        var openedAt = request.OpenedAt?.ToUniversalTime();
        if (openedAt is not null && openedAt.Value > DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("La fecha y hora de apertura no pueden estar en el futuro.");
        }

        if (await _shifts.GetOpenShiftAsync(request.CashierUserId, ct).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException("Este cajero ya tiene un turno abierto.");
        }

        var shift = new CashierShift(
            request.CashierUserId,
            request.CashierUsername,
            new Money(request.OpeningCash),
            request.Notes,
            openedAt);
        await _shifts.AddAsync(shift, ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        return ToSummary(shift);
    }

    internal static ShiftSummary ToSummary(CashierShift shift) => new(
        shift.Id,
        shift.CashierUserId,
        shift.CashierUsername,
        shift.OpeningCash.Amount,
        shift.Status == Domain.Enums.ShiftStatus.Closed ? shift.ClosingCash.Amount : null,
        shift.Status,
        shift.OpenedAt,
        shift.ClosedAt,
        shift.Notes,
        shift.HasClosingSummary,
        shift.SalesTotal.Amount,
        shift.CashPayments.Amount,
        shift.DigitalPayments.Amount,
        shift.ReceivableSettlementCash.Amount,
        shift.ReceivableSettlementDigital.Amount);
}
