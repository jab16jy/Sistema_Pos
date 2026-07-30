using POS.Application.DTOs.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Loyalty;

public sealed class GetLoyaltyBenefitsUseCase(ILoyaltyRepository loyalty)
{
    public async Task<IReadOnlyList<LoyaltyBenefitResult>> ExecuteAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
        => (await loyalty.GetBenefitsAsync(activeOnly, cancellationToken).ConfigureAwait(false))
            .Select(LoyaltyBenefitMapper.ToResult)
            .ToList();
}

public sealed class SaveLoyaltyBenefitUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork)
{
    public async Task<LoyaltyBenefitResult> ExecuteAsync(SaveLoyaltyBenefitRequest request, CancellationToken cancellationToken = default)
    {
        LoyaltyBenefit benefit;
        if (request.BenefitId is { } benefitId)
        {
            benefit = await loyalty.GetBenefitByIdAsync(benefitId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("El beneficio seleccionado ya no existe.");
            benefit.Update(request.Name, request.PointsCost, request.Description);
            ApplyActiveFlag(benefit, request.IsActive);
            await loyalty.UpdateBenefitAsync(benefit, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            benefit = new LoyaltyBenefit(request.Name, request.PointsCost, request.Description);
            ApplyActiveFlag(benefit, request.IsActive);
            await loyalty.AddBenefitAsync(benefit, cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LoyaltyBenefitMapper.ToResult(benefit);
    }

    private static void ApplyActiveFlag(LoyaltyBenefit benefit, bool isActive)
    {
        if (isActive)
        {
            benefit.Activate();
        }
        else
        {
            benefit.Deactivate();
        }
    }
}

public sealed class DeleteLoyaltyBenefitUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork)
{
    /// <returns>True when the benefit was removed, false when it was deactivated because it had claims.</returns>
    public async Task<bool> ExecuteAsync(Guid benefitId, CancellationToken cancellationToken = default)
    {
        if (benefitId == Guid.Empty)
        {
            throw new ArgumentException("A benefit must be selected.", nameof(benefitId));
        }

        // A claimed reward has to stay in the catalogue — customers' claim rows point at it, and the
        // database FK refuses the delete anyway. Deactivating is the honest equivalent.
        if (await loyalty.HasClaimsForBenefitAsync(benefitId, cancellationToken).ConfigureAwait(false))
        {
            LoyaltyBenefit claimed = await loyalty.GetBenefitByIdAsync(benefitId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("El beneficio seleccionado ya no existe.");
            claimed.Deactivate();
            await loyalty.UpdateBenefitAsync(claimed, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await loyalty.DeleteBenefitAsync(benefitId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}

internal static class LoyaltyBenefitMapper
{
    public static LoyaltyBenefitResult ToResult(LoyaltyBenefit benefit)
        => new(benefit.Id, benefit.Name, benefit.Description, benefit.PointsCost, benefit.IsActive);

    public static LoyaltyBenefitClaimResult ToResult(LoyaltyBenefitClaim claim)
        => new(
            claim.Id,
            claim.LoyaltyBenefitId,
            claim.BenefitName,
            claim.PointsSpent,
            claim.Status,
            claim.ClaimedAt,
            claim.DeliveredAt,
            claim.CancelledAt,
            claim.CancellationReason);
}
