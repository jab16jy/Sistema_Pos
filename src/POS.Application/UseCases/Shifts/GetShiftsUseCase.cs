using POS.Application.DTOs.Shifts;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Shifts;

public sealed class GetShiftsUseCase
{
    private readonly ICashierShiftRepository _shifts;

    public GetShiftsUseCase(ICashierShiftRepository shifts) => _shifts = shifts;

    public async Task<IReadOnlyList<ShiftSummary>> ExecuteAsync(int limit = 50, DateTimeOffset? fromDate = null, DateTimeOffset? toDate = null, CancellationToken ct = default)
    {
        var shifts = await _shifts.GetRecentAsync(limit, fromDate, toDate, ct).ConfigureAwait(false);
        return shifts.Select(StartShiftUseCase.ToSummary).ToList();
    }
}
