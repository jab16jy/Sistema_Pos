using POS.Application.DTOs.Shifts;
using POS.Application.Services;
using POS.Domain.Authorization;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Shifts;

public sealed class CloseShiftUseCase
{
    private readonly ICashierShiftRepository _shifts;
    private readonly ISaleRepository _sales;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CriticalCommandAuthorizer _authorizer;

    public CloseShiftUseCase(
        ICashierShiftRepository shifts,
        ISaleRepository sales,
        IUnitOfWork unitOfWork,
        CriticalCommandAuthorizer authorizer)
    {
        _shifts = shifts;
        _sales = sales ?? throw new ArgumentNullException(nameof(sales));
        _unitOfWork = unitOfWork;
        _authorizer = authorizer;
    }

    public async Task<ShiftSummary> ExecuteAsync(CloseShiftRequest request, CancellationToken ct = default)
    {
        CanonicalActor actor = await _authorizer.AuthorizeAsync(PermissionKeys.Shifts.Close, ct).ConfigureAwait(false);
        var shift = await _shifts.GetByIdAsync(request.ShiftId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Turno no encontrado.");

        if (shift.CashierUserId != actor.UserId)
        {
            throw new UnauthorizedActionException(PermissionKeys.Shifts.Close);
        }

        DateTimeOffset closedAt = DateTimeOffset.UtcNow;
        ShiftSalesSummaryData salesSummary = await _sales
            .GetShiftSalesSummaryAsync(shift.CashierUserId, shift.OpenedAt, closedAt, ct)
            .ConfigureAwait(false);

        shift.Close(
            new Money(request.ClosingCash),
            new ShiftClosingSummary(
                new Money(salesSummary.SalesTotal),
                new Money(salesSummary.CashPayments),
                new Money(salesSummary.DigitalPayments),
                new Money(salesSummary.ReceivableSettlementCash),
                new Money(salesSummary.ReceivableSettlementDigital),
                new Money(salesSummary.CashReturnOutflows),
                new Money(salesSummary.DigitalReturnOutflows)),
            closedAt,
            request.Notes);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        return StartShiftUseCase.ToSummary(shift);
    }
}
