using POS.Application.DTOs.Shifts;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Shifts;

public sealed class CloseShiftUseCase
{
    private readonly ICashierShiftRepository _shifts;
    private readonly IUnitOfWork _unitOfWork;

    public CloseShiftUseCase(ICashierShiftRepository shifts, IUnitOfWork unitOfWork)
    {
        _shifts = shifts;
        _unitOfWork = unitOfWork;
    }

    public async Task<ShiftSummary> ExecuteAsync(CloseShiftRequest request, CancellationToken ct = default)
    {
        var shift = await _shifts.GetByIdAsync(request.ShiftId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Turno no encontrado.");

        shift.Close(new Money(request.ClosingCash), request.Notes);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
        return StartShiftUseCase.ToSummary(shift);
    }
}
