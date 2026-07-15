using POS.Application.DTOs.Sales;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Sales;

public sealed class GetHeldSalesUseCase
{
    private readonly ISaleRepository _sales;

    public GetHeldSalesUseCase(ISaleRepository sales) => _sales = sales;

    public async Task<IReadOnlyList<HeldSaleSummary>> ExecuteAsync(
        Guid cashierUserId,
        CancellationToken cancellationToken = default)
    {
        if (cashierUserId == Guid.Empty)
        {
            return [];
        }

        IReadOnlyList<Domain.Entities.Sale> sales = await _sales
            .GetOnHoldByCashierAsync(cashierUserId, cancellationToken)
            .ConfigureAwait(false);
        return sales.Select(HeldSaleMapper.Map).ToArray();
    }
}
