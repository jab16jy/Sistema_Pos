using POS.Application.DTOs.Quotes;
using POS.Application.DTOs.Sales;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Sales;

internal static class CashierDiscountPolicy
{
    public static async Task<decimal> GetMaximumAsync(
        ISettingsRepository? settingsRepository,
        CancellationToken cancellationToken)
    {
        if (settingsRepository is null)
        {
            return BusinessSettings.DefaultMaximumCashierDiscountPercent;
        }

        BusinessSettings? settings = await settingsRepository.GetAsync(cancellationToken).ConfigureAwait(false);
        return settings?.MaximumCashierDiscountPercent ?? BusinessSettings.DefaultMaximumCashierDiscountPercent;
    }

    public static void Validate(IReadOnlyCollection<CreateSaleItemRequest> items, decimal maximumPercent)
    {
        if (maximumPercent < 0m || maximumPercent > 100m)
        {
            throw new InvalidOperationException("The configured maximum cashier discount must be between 0 and 100 percent.");
        }

        foreach (CreateSaleItemRequest item in items)
        {
            if (item.ManualDiscountPercent < 0m || item.ManualDiscountPercent > 100m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(item.ManualDiscountPercent),
                    "Manual line discount must be between 0 and 100 percent.");
            }

            if (item.ManualDiscountPercent > maximumPercent)
            {
                throw new InvalidOperationException(
                    $"Manual discount for product '{item.ProductId}' exceeds the configured maximum of {maximumPercent:0.##}%.");
            }
        }
    }

    public static void Validate(IReadOnlyCollection<CreateQuoteItemRequest> items, decimal maximumPercent)
    {
        if (maximumPercent < 0m || maximumPercent > 100m)
        {
            throw new InvalidOperationException("The configured maximum cashier discount must be between 0 and 100 percent.");
        }

        foreach (CreateQuoteItemRequest item in items)
        {
            if (item.Discount < 0m || item.Discount > 100m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(item.Discount),
                    "Manual line discount must be between 0 and 100 percent.");
            }

            if (item.Discount > maximumPercent)
            {
                throw new InvalidOperationException(
                    $"Manual discount for product '{item.ProductId}' exceeds the configured maximum of {maximumPercent:0.##}%.");
            }
        }
    }
}
