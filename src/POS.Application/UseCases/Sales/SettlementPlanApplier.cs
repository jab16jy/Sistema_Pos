using POS.Application.Common;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Sales;

/// <summary>
/// Applies a <see cref="SaleSettlementPlan"/>'s collected legs to a <see cref="Sale"/>: validates
/// each leg's payment platform and adds the matching Payment. Shared by CreateSaleUseCase and
/// CompleteHeldSaleUseCase so the per-leg loop lives in exactly one place.
/// </summary>
internal static class SettlementPlanApplier
{
    public static void Apply(Sale sale, SaleSettlementPlan plan)
    {
        foreach (SettlementCollectedPayment leg in plan.CollectedPayments)
        {
            PaymentPlatforms.Validate(leg.Method, leg.BankOrPlatform);
            sale.AddPayment(new Payment(
                leg.Method,
                new Money(leg.Amount),
                leg.Method == PaymentMethod.Cash ? new Money(leg.Amount) : null,
                leg.BankOrPlatform));
        }
    }
}
