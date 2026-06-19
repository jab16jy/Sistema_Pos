using System.Diagnostics;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

public sealed class WhatsAppScriptService : IWhatsAppService
{
    public async Task<bool> SendAsync(
        string phoneNumber,
        string message,
        string? attachmentPath,
        CancellationToken ct = default)
    {
        var scriptPath = Path.GetFullPath(
            Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "..", "scripts", "send-whatsapp.sh"));

        if (!File.Exists(scriptPath))
        {
            return false;
        }

        var args = $"\"{phoneNumber}\" \"{message}\"";
        if (!string.IsNullOrEmpty(attachmentPath))
        {
            args += $" \"{attachmentPath}\"";
        }

        var psi = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"{scriptPath} {args}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            return false;
        }

        await process.WaitForExitAsync(ct);
        return process.ExitCode == 0;
    }
}
