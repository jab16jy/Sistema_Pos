using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Settings;

public sealed class SaveSettingsUseCase
{
    private readonly ISettingsRepository _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPrinterCommandValidator? _printerCommandValidator;
    private readonly ICurrentOperatorSession _session;

    public SaveSettingsUseCase(
        ISettingsRepository settings,
        IUnitOfWork unitOfWork,
        ICurrentOperatorSession session,
        IPrinterCommandValidator? printerCommandValidator = null)
    {
        _settings = settings;
        _unitOfWork = unitOfWork;
        _printerCommandValidator = printerCommandValidator;
        _session = session;
    }

    public async Task<SettingsResult> ExecuteAsync(SaveSettingsRequest request, CancellationToken cancellationToken = default)
    {
        if (PermissionAuthorization.EnsureHasPermission(_session, PermissionKeys.Settings.Edit, "Unauthorized") is not null)
        {
            throw new UnauthorizedActionException("SaveSettings");
        }

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
                request.PrinterPort,
                request.BackupDirectory,
                request.CurrencyCode,
                request.MaximumCashierDiscountPercent,
                request.MaximumDeferredBalancePercent);
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
                request.PrinterPort,
                request.BackupDirectory,
                request.CurrencyCode,
                request.MaximumCashierDiscountPercent,
                request.MaximumDeferredBalancePercent);
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
            settings.PrinterPort,
            settings.BackupDirectory,
            settings.CurrencyCode,
            warnings,
            settings.LastBackupAtUtc is null || settings.LastBackupSizeBytes is null || settings.LastBackupLocation is null
                ? null
                : new ManualBackupMetadata(
                    settings.LastBackupAtUtc.Value,
                    settings.LastBackupSizeBytes.Value,
                    settings.LastBackupLocation,
                    settings.LastBackupUsername),
            settings.MaximumCashierDiscountPercent,
            settings.MaximumDeferredBalancePercent);
    }
}
