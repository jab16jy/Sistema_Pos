using POS.Application.DTOs.Settings;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

public sealed class PrinterCommandValidator : IPrinterCommandValidator
{
    public Task<IReadOnlyList<SettingsWarning>> ValidateAsync(
        string? printerCommand,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(printerCommand))
        {
            return Task.FromResult<IReadOnlyList<SettingsWarning>>([]);
        }

        string command = printerCommand.Trim();
        if (Path.IsPathRooted(command) && !File.Exists(command))
        {
            return Task.FromResult<IReadOnlyList<SettingsWarning>>(
            [
                new SettingsWarning(
                    "printer_command_not_found",
                    "The configured printer command was not found. Settings were saved so the printer can be configured later.")
            ]);
        }

        return Task.FromResult<IReadOnlyList<SettingsWarning>>([]);
    }
}
