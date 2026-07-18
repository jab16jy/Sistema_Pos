using POS.Application.DTOs.Shifts;
using POS.Application.Services;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Shifts;

public sealed class StartShiftUseCase
{
    private readonly ICashierShiftRepository _shifts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CriticalCommandAuthorizer _authorizer;

    public StartShiftUseCase(
        ICashierShiftRepository shifts,
        IUnitOfWork unitOfWork,
        CriticalCommandAuthorizer authorizer)
    {
        _shifts = shifts;
        _unitOfWork = unitOfWork;
        _authorizer = authorizer;
    }

    public async Task<ShiftSummary> ExecuteAsync(StartShiftRequest request, CancellationToken ct = default)
    {
        CanonicalActor actor = await _authorizer.AuthorizeAsync(PermissionKeys.Shifts.Open, ct).ConfigureAwait(false);
        var openedAt = request.OpenedAt?.ToUniversalTime();
        if (openedAt is not null && openedAt.Value > DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("La fecha y hora de apertura no pueden estar en el futuro.");
        }

        if (await _shifts.GetOpenShiftAsync(actor.UserId, ct).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException("Este cajero ya tiene un turno abierto.");
        }

        var shift = new CashierShift(
            actor.UserId,
            actor.Username,
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
        shift.ReceivableSettlementDigital.Amount,
        shift.CashReturnOutflows.Amount,
        shift.DigitalReturnOutflows.Amount);
}
