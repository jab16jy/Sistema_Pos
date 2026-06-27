using POS.Application.DTOs.Settings;

namespace POS.Application.Interfaces;

public interface IPrinterCommandValidator
{
    Task<IReadOnlyList<SettingsWarning>> ValidateAsync(
        string? printerCommand,
        CancellationToken cancellationToken = default);
}
