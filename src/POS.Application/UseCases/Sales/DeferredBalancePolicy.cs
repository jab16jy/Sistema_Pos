using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Sales;

internal static class DeferredBalancePolicy
{
    public static async Task<decimal> GetMaximumAsync(
        ISettingsRepository? settingsRepository,
        CancellationToken cancellationToken)
    {
        if (settingsRepository is null)
        {
            return BusinessSettings.DefaultMaximumDeferredBalancePercent;
        }

        BusinessSettings? settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        return settings?.MaximumDeferredBalancePercent ?? BusinessSettings.DefaultMaximumDeferredBalancePercent;
    }

    public static void Validate(decimal deferredBalanceAmount, decimal saleTotal, decimal maximumPercent)
    {
        if (deferredBalanceAmount <= 0m)
        {
            return;
        }

        decimal maximumDeferred = decimal.Round(saleTotal * maximumPercent / 100m, 2, MidpointRounding.AwayFromZero);
        if (deferredBalanceAmount > maximumDeferred)
        {
            throw new InvalidOperationException(
                $"El saldo diferido no puede superar el {maximumPercent:0.##}% del total de la venta ({maximumDeferred:0.##}).");
        }
    }
}
