using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Products;

internal static class ProductPresentationCountPolicy
{
    public static async Task<int> GetMaximumAsync(
        ISettingsRepository? settingsRepository,
        CancellationToken cancellationToken)
    {
        if (settingsRepository is null)
        {
            return BusinessSettings.DefaultMaximumProductPresentations;
        }

        BusinessSettings? settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        return settings?.MaximumProductPresentations ?? BusinessSettings.DefaultMaximumProductPresentations;
    }

    public static void Validate(int presentationCount, int maximum)
    {
        if (presentationCount > maximum)
        {
            throw new InvalidOperationException(
                $"Un producto no puede tener más de {maximum} presentaciones (tiene {presentationCount}). Reduce la cantidad para no saturar las opciones en Caja.");
        }
    }
}
