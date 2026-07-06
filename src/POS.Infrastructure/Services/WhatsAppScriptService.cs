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

        ProcessStartInfo psi = CreateStartInfo(_options.FileName, phoneNumber, message, attachmentPath);

        if (!string.IsNullOrWhiteSpace(_options.WorkingDirectory))
        {
            psi.WorkingDirectory = _options.WorkingDirectory;
        }

        using var process = Process.Start(psi);
        if (process is null)
        {
            return false;
        }

        await process.WaitForExitAsync(ct);
        return process.ExitCode == 0;
    }

    private static ProcessStartInfo CreateStartInfo(
        string scriptPathOrCommand,
        string phoneNumber,
        string message,
        string? attachmentPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = scriptPathOrCommand,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        string extension = Path.GetExtension(scriptPathOrCommand);
        if (Path.IsPathRooted(scriptPathOrCommand) && File.Exists(scriptPathOrCommand))
        {
            if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
            {
                psi.FileName = "cmd.exe";
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add("call");
                psi.ArgumentList.Add(scriptPathOrCommand);
            }
            else if (extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
            {
                psi.FileName = "powershell.exe";
                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-ExecutionPolicy");
                psi.ArgumentList.Add("Bypass");
                psi.ArgumentList.Add("-File");
                psi.ArgumentList.Add(scriptPathOrCommand);
            }
            else if (extension.Equals(".py", StringComparison.OrdinalIgnoreCase))
            {
                psi.FileName = "python";
                psi.ArgumentList.Add(scriptPathOrCommand);
            }
        }

        psi.ArgumentList.Add(phoneNumber);
        if (!string.IsNullOrWhiteSpace(attachmentPath))
        {
            psi.ArgumentList.Add(attachmentPath);
        }
        psi.ArgumentList.Add(message);

        return psi;
    }
}
