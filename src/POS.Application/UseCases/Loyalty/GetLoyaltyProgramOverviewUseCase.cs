using POS.Application.DTOs.Loyalty;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Loyalty;

public sealed class GetLoyaltyProgramOverviewUseCase
{
    private readonly ILoyaltyRepository _loyalty;

    public GetLoyaltyProgramOverviewUseCase(ILoyaltyRepository loyalty)
    {
        _loyalty = loyalty;
    }

    public async Task<LoyaltyProgramOverviewResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var activeRule = await _loyalty.GetActiveRuleAsync(cancellationToken).ConfigureAwait(false);
        var recentTransactions = await _loyalty.GetRecentProgramTransactionDetailsAsync(12, cancellationToken).ConfigureAwait(false);

        return new LoyaltyProgramOverviewResult(
            activeRule is null
                ? null
                : new LoyaltyRuleResult(
                    activeRule.Id,
                    activeRule.PointsPerCurrencyUnit,
                    activeRule.RedemptionValuePerPoint,
                    activeRule.MinimumRedeemPoints,
                    activeRule.Description,
                    activeRule.IsActive,
                    activeRule.CreatedAt),
            await _loyalty.GetTotalIssuedPointsAsync(cancellationToken).ConfigureAwait(false),
            await _loyalty.GetEnrolledCustomerCountAsync(cancellationToken).ConfigureAwait(false),
            await _loyalty.GetRedemptionCountAsync(monthStart, now, cancellationToken).ConfigureAwait(false),
            recentTransactions
                .Select(details => new LoyaltyTransactionResult(
                    details.Transaction.Id,
                    details.Transaction.Points,
                    details.Transaction.Type.ToString(),
                    details.Transaction.SaleId,
                    details.Transaction.Description,
                    details.Transaction.CreatedAt,
                    details.CustomerName,
                    details.InvoiceNumber,
                    details.ResponsibleUser))
                .ToList());
    }
}
