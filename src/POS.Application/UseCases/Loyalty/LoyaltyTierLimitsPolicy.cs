using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Loyalty;

/// <summary>
/// Business limits a loyalty tier's automatic benefits must respect.
/// </summary>
/// <remarks>
/// A tier discount is applied to the whole sale without the cashier touching anything, so it is
/// bound by the same ceiling as a manual cashier discount — otherwise a tier would be a way to hand
/// out discounts the business explicitly capped. The multiplier has its own ceiling because it
/// inflates point liability rather than the sale total.
/// Mirrors CashierDiscountPolicy/DeferredBalancePolicy: read the ceiling from settings, fall back to
/// the domain default when settings are unavailable.
/// </remarks>
internal static class LoyaltyTierLimitsPolicy
{
    public static async Task<(decimal MaximumDiscountPercent, decimal MaximumPointsMultiplier)> GetLimitsAsync(
        ISettingsRepository? settingsRepository,
        CancellationToken cancellationToken)
    {
        if (settingsRepository is null)
        {
            return (BusinessSettings.DefaultMaximumCashierDiscountPercent, BusinessSettings.DefaultMaximumPointsMultiplier);
        }

        BusinessSettings? settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        return (
            settings?.MaximumCashierDiscountPercent ?? BusinessSettings.DefaultMaximumCashierDiscountPercent,
            settings?.MaximumPointsMultiplier ?? BusinessSettings.DefaultMaximumPointsMultiplier);
    }

    public static void Validate(
        decimal discountPercent,
        decimal pointsMultiplier,
        decimal maximumDiscountPercent,
        decimal maximumPointsMultiplier)
    {
        if (discountPercent > maximumDiscountPercent)
        {
            throw new InvalidOperationException(
                $"El descuento del nivel no puede superar el máximo configurado de {maximumDiscountPercent:0.##}%.");
        }

        if (pointsMultiplier > maximumPointsMultiplier)
        {
            throw new InvalidOperationException(
                $"El multiplicador de puntos del nivel no puede superar el máximo configurado de {maximumPointsMultiplier:0.##}x.");
        }
    }

    public static async Task ValidateAsync(
        ISettingsRepository? settingsRepository,
        decimal discountPercent,
        decimal pointsMultiplier,
        CancellationToken cancellationToken)
    {
        var (maximumDiscount, maximumMultiplier) = await GetLimitsAsync(settingsRepository, cancellationToken).ConfigureAwait(false);
        Validate(discountPercent, pointsMultiplier, maximumDiscount, maximumMultiplier);
    }
}
