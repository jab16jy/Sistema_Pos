using POS.Application.DTOs.Settings;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Settings;

public sealed class LoadSettingsUseCase
{
    private readonly ISettingsRepository _settings;

    public LoadSettingsUseCase(ISettingsRepository settings)
    {
        _settings = settings;
    }

    public async Task<SettingsResult?> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        BusinessSettings? settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        return settings is null ? null : ToResult(settings);
    }

    private static SettingsResult ToResult(BusinessSettings settings) => new(
        settings.Id,
        settings.BusinessName,
        settings.BusinessAddress,
        settings.TaxId,
        settings.PrinterCommand,
        settings.PrinterWorkingDirectory,
        settings.BackupDirectory,
        settings.CurrencyCode,
        []);
}
