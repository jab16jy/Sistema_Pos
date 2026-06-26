using System.Diagnostics;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

public sealed class WhatsAppScriptService : IWhatsAppService
{
    private readonly WhatsAppScriptRuntimeOptions _options;

    public WhatsAppScriptService(WhatsAppScriptRuntimeOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<bool> SendAsync(
        string phoneNumber,
        string message,
        string? attachmentPath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.FileName))
        {
            throw new InvalidOperationException("El envío por WhatsApp no está configurado. Ingresa la ruta del script en Ajustes.");
        }

        if (Path.IsPathRooted(_options.FileName) && !File.Exists(_options.FileName))
        {
            throw new InvalidOperationException("El script de WhatsApp configurado no existe. Revisa la ruta en Ajustes.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = _options.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (!string.IsNullOrWhiteSpace(_options.WorkingDirectory))
        {
            psi.WorkingDirectory = _options.WorkingDirectory;
        }

        psi.ArgumentList.Add(phoneNumber);
        psi.ArgumentList.Add(message);
        if (!string.IsNullOrWhiteSpace(attachmentPath))
        {
            psi.ArgumentList.Add(attachmentPath);
        }

        using var process = Process.Start(psi);
        if (process is null)
        {
            return false;
        }

        await process.WaitForExitAsync(ct);
        return process.ExitCode == 0;
    }
}
