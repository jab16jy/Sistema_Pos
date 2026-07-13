using POS.Application.Interfaces;
using POS.Application.UseCases.Auth;
using POS.Domain.Authorization;
using POS.Domain.Entities;
using POS.Domain.Exceptions;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Settings;

/// <summary>
/// Persists just the backup destination directory, preserving every other business setting.
/// Lets the Respaldos module remember the folder without owning the full business-settings form.
/// </summary>
public sealed class UpdateBackupDirectoryUseCase
{
    private readonly ISettingsRepository _settings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentOperatorSession _session;

    public UpdateBackupDirectoryUseCase(ISettingsRepository settings, IUnitOfWork unitOfWork, ICurrentOperatorSession session)
    {
        _settings = settings;
        _unitOfWork = unitOfWork;
        _session = session;
    }

    /// <returns>False when no business settings exist yet (nothing to update).</returns>
    public async Task<bool> ExecuteAsync(string? backupDirectory, CancellationToken cancellationToken = default)
    {
        if (PermissionAuthorization.EnsureHasPermission(_session, PermissionKeys.Settings.Edit, "Unauthorized") is not null)
        {
            throw new UnauthorizedActionException("UpdateBackupDirectory");
        }

        BusinessSettings? settings = await _settings.GetAsync(cancellationToken).ConfigureAwait(false);
        if (settings is null)
        {
            return false;
        }

        settings.Update(
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
            backupDirectory,
            settings.CurrencyCode);

        await _settings.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
