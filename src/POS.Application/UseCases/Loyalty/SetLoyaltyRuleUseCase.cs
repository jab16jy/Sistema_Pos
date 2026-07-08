using POS.Application.DTOs.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Loyalty;

public sealed class SetLoyaltyRuleUseCase
{
    private readonly ILoyaltyRepository _loyalty;
    private readonly IUnitOfWork _unitOfWork;

    public SetLoyaltyRuleUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork)
    {
        _loyalty = loyalty;
        _unitOfWork = unitOfWork;
    }

    public async Task<LoyaltyRuleResult> ExecuteAsync(SetLoyaltyRuleRequest request, CancellationToken cancellationToken = default)
    {
        var current = await _loyalty.GetActiveRuleAsync(cancellationToken).ConfigureAwait(false);
        if (current is not null)
        {
            current.Deactivate();
            await _loyalty.UpdateRuleAsync(current, cancellationToken).ConfigureAwait(false);
        }

        var next = new LoyaltyRule(
            request.PointsPerCurrencyUnit,
            request.RedemptionValuePerPoint,
            request.MinimumRedeemPoints,
            request.Description);

        await _loyalty.AddRuleAsync(next, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new LoyaltyRuleResult(
            next.Id,
            next.PointsPerCurrencyUnit,
            next.RedemptionValuePerPoint,
            next.MinimumRedeemPoints,
            next.Description,
            next.IsActive,
            next.CreatedAt);
    }
}
