using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Sales;

/// <summary>
/// Read-only lookup of the deferred balance cap so the checkout screen can mirror
/// <see cref="DeferredBalancePolicy"/> live, before the cashier attempts to confirm the sale.
/// </summary>
public sealed class GetDeferredBalanceLimitUseCase
{
    private readonly ISettingsRepository? _settings;

    public GetDeferredBalanceLimitUseCase(ISettingsRepository? settings = null)
    {
        _settings = settings;
    }

    public Task<decimal> ExecuteAsync(CancellationToken cancellationToken = default)
        => DeferredBalancePolicy.GetMaximumAsync(_settings, cancellationToken);
}
