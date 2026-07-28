using POS.Application.DTOs.Loyalty;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Loyalty;

public sealed class GetLoyaltyTiersUseCase(ILoyaltyRepository loyalty)
{
    public async Task<IReadOnlyList<LoyaltyTierResult>> ExecuteAsync(CancellationToken cancellationToken = default)
        => (await loyalty.GetTiersAsync(cancellationToken).ConfigureAwait(false))
            .Select(LoyaltyTierMapper.ToResult)
            .ToList();
}

/// <summary>
/// Exposes the tier limits so the editor can show them and block an invalid value before saving.
/// Deliberately ungated: these are typing constraints, not sensitive configuration, and the loyalty
/// operator may not hold the Settings.View permission that LoadSettingsUseCase requires.
/// </summary>
public sealed class GetLoyaltyTierLimitsUseCase(ISettingsRepository? settings = null)
{
    public async Task<LoyaltyTierLimitsResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var (maximumDiscount, maximumMultiplier) = await LoyaltyTierLimitsPolicy
            .GetLimitsAsync(settings, cancellationToken)
            .ConfigureAwait(false);

        return new LoyaltyTierLimitsResult(maximumDiscount, maximumMultiplier);
    }
}

public sealed class CreateLoyaltyTierUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork, ISettingsRepository? settings = null)
{
    public async Task<LoyaltyTierResult> ExecuteAsync(CreateLoyaltyTierRequest request, CancellationToken cancellationToken = default)
    {
        await LoyaltyTierLimitsPolicy
            .ValidateAsync(settings, request.DiscountPercent, request.PointsMultiplier, cancellationToken)
            .ConfigureAwait(false);

        var tier = new LoyaltyTier(request.Name, request.MinPoints, request.DiscountPercent, request.PointsMultiplier, request.Color, request.TextForeground, request.Benefits, request.AllowsRedemption);
        await loyalty.AddTierAsync(tier, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LoyaltyTierMapper.ToResult(tier);
    }
}

public sealed class UpdateLoyaltyTierUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork, ISettingsRepository? settings = null)
{
    public async Task<LoyaltyTierResult> ExecuteAsync(UpdateLoyaltyTierRequest request, CancellationToken cancellationToken = default)
    {
        await LoyaltyTierLimitsPolicy
            .ValidateAsync(settings, request.DiscountPercent, request.PointsMultiplier, cancellationToken)
            .ConfigureAwait(false);

        LoyaltyTier tier = await loyalty.GetTierByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("El tier seleccionado ya no existe.");
        tier.Update(request.Name, request.MinPoints, request.DiscountPercent, request.PointsMultiplier, request.Color, request.TextForeground, request.Benefits, request.AllowsRedemption);
        await loyalty.UpdateTierAsync(tier, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LoyaltyTierMapper.ToResult(tier);
    }
}

public sealed class DeleteLoyaltyTierUseCase(ILoyaltyRepository loyalty, IUnitOfWork unitOfWork)
{
    public async Task ExecuteAsync(Guid tierId, CancellationToken cancellationToken = default)
    {
        if (tierId == Guid.Empty)
        {
            throw new ArgumentException("A tier must be selected.", nameof(tierId));
        }

        await loyalty.DeleteTierAsync(tierId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal static class LoyaltyTierMapper
{
    public static LoyaltyTierResult ToResult(LoyaltyTier tier)
        => new(tier.Id, tier.Name, tier.MinPoints, tier.DiscountPercent, tier.PointsMultiplier, tier.Color, tier.TextForeground, tier.Benefits, tier.AllowsRedemption);
}
