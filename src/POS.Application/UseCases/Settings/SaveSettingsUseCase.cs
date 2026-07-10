using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Settings;

public sealed class SaveSettingsUseCase
{
    private readonly ISettingsRepository _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPrinterCommandValidator? _printerCommandValidator;

    public SaveSettingsUseCase(
        ISettingsRepository settings,
        IUnitOfWork unitOfWork,
        IPrinterCommandValidator? printerCommandValidator = null)
    {
        _settings = settings;
        _unitOfWork = unitOfWork;
        _printerCommandValidator = printerCommandValidator;
    }

    public async Task<SettingsResult> ExecuteAsync(SaveSettingsRequest request, CancellationToken cancellationToken = default)
    {
        BusinessSettings? settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        if (settings is null)
        {
            settings = new BusinessSettings(
                request.BusinessName,
                request.BusinessAddress,
                request.TaxId,
                request.BusinessPhone,
                request.SmtpHost,
                request.SmtpPort,
                request.SmtpUser,
                request.SmtpPassword,
                request.SmtpUseSsl,
                request.SmtpSenderAddress,
                request.PrinterCommand,
                request.PrinterWorkingDirectory,
                request.BackupDirectory,
                request.CurrencyCode);
        }
        else
        {
            settings.Update(
                request.BusinessName,
                request.BusinessAddress,
                request.TaxId,
                request.BusinessPhone,
                request.SmtpHost,
                request.SmtpPort,
                request.SmtpUser,
                request.SmtpPassword,
                request.SmtpUseSsl,
                request.SmtpSenderAddress,
                request.PrinterCommand,
                request.PrinterWorkingDirectory,
                request.BackupDirectory,
                request.CurrencyCode);
        }

        IReadOnlyList<SettingsWarning> warnings = _printerCommandValidator is null
            ? []
            : await _printerCommandValidator.ValidateAsync(settings.PrinterCommand, cancellationToken).ConfigureAwait(false);

        await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SettingsResult(
            settings.Id,
            settings.BusinessName,
            settings.BusinessAddress,
            settings.TaxId,
            settings.BusinessPhone,
            settings.SmtpHost,
            settings.SmtpPort,
            settings.SmtpUser,
            settings.SmtpPassword,
            settings.SmtpUseSsl,
            settings.SmtpSenderAddress,
            settings.PrinterCommand,
            settings.PrinterWorkingDirectory,
            settings.BackupDirectory,
            settings.CurrencyCode,
            warnings,
            settings.LastBackupAtUtc is null || settings.LastBackupSizeBytes is null || settings.LastBackupLocation is null
                ? null
                : new ManualBackupMetadata(
                    settings.LastBackupAtUtc.Value,
                    settings.LastBackupSizeBytes.Value,
                    settings.LastBackupLocation,
                    settings.LastBackupUsername));
    }
}
