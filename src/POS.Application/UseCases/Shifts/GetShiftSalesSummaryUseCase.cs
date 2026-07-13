using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Shifts;

public sealed class GetShiftSalesSummaryUseCase
{
    private readonly ISaleRepository _sales;

    public GetShiftSalesSummaryUseCase(ISaleRepository sales) => _sales = sales;

    public Task<ShiftSalesSummaryData> ExecuteAsync(
        Guid cashierUserId,
        DateTimeOffset openedAt,
        CancellationToken cancellationToken = default)
        => _sales.GetShiftSalesSummaryAsync(cashierUserId, openedAt, cancellationToken);

    public Task<ShiftSalesSummaryData> ExecuteAsync(
        Guid cashierUserId,
        DateTimeOffset openedAt,
        DateTimeOffset closedAt,
        CancellationToken cancellationToken = default)
        => _sales.GetShiftSalesSummaryAsync(cashierUserId, openedAt, closedAt, cancellationToken);
}
