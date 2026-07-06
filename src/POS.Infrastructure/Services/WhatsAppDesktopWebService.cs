using System.Diagnostics;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

public interface IExternalLinkLauncher
{
    bool TryOpen(Uri uri);
    void RevealFile(string filePath);
}

public sealed class ExternalLinkLauncher : IExternalLinkLauncher
{
    public bool TryOpen(Uri uri)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = uri.ToString(),
                UseShellExecute = true
            });

            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    public void RevealFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }
}

public sealed class WhatsAppDesktopWebService : IWhatsAppService
{
    private readonly IExternalLinkLauncher _launcher;

    public WhatsAppDesktopWebService(IExternalLinkLauncher? launcher = null)
    {
        _launcher = launcher ?? new ExternalLinkLauncher();
    }

    public Task<bool> SendAsync(
        string phoneNumber,
        string message,
        string? attachmentPath,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        string normalizedPhone = NormalizePhoneNumber(phoneNumber);
        if (normalizedPhone.Length < 8)
        {
            throw new InvalidOperationException("Ingresa un número de WhatsApp válido antes de enviar.");
        }

        string finalMessage = BuildMessage(message, attachmentPath);
        Uri desktopUri = BuildDesktopUri(normalizedPhone, finalMessage);
        Uri webUri = BuildWebUri(normalizedPhone, finalMessage);

        bool opened = _launcher.TryOpen(desktopUri) || _launcher.TryOpen(webUri);
        if (!opened)
        {
            throw new InvalidOperationException("No se pudo abrir WhatsApp Desktop ni WhatsApp Web en este equipo.");
        }

        if (!string.IsNullOrWhiteSpace(attachmentPath))
        {
            _launcher.RevealFile(attachmentPath);
        }

        return Task.FromResult(true);
    }

    public static string NormalizePhoneNumber(string phoneNumber) =>
        new(phoneNumber.Where(char.IsDigit).ToArray());

    public static string BuildMessage(string message, string? attachmentPath)
    {
        string normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? "Hola."
            : message.Trim();

        if (string.IsNullOrWhiteSpace(attachmentPath))
        {
            return normalizedMessage;
        }

        return $"{normalizedMessage}\n\nArchivo listo para adjuntar:\n{attachmentPath}";
    }

    public static Uri BuildDesktopUri(string phoneNumber, string message) =>
        new($"whatsapp://send?phone={Uri.EscapeDataString(phoneNumber)}&text={Uri.EscapeDataString(message)}");

    public static Uri BuildWebUri(string phoneNumber, string message) =>
        new($"https://wa.me/{Uri.EscapeDataString(phoneNumber)}?text={Uri.EscapeDataString(message)}");
}
