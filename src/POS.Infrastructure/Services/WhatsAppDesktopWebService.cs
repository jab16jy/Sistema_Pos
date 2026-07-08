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
    private readonly Func<string> _documentsDirectoryProvider;

    public WhatsAppDesktopWebService(
        IExternalLinkLauncher? launcher = null,
        Func<string>? documentsDirectoryProvider = null)
    {
        _launcher = launcher ?? new ExternalLinkLauncher();
        _documentsDirectoryProvider = documentsDirectoryProvider
            ?? (() => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
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

        string? shareableAttachmentPath = PrepareAttachmentForManualAttach(attachmentPath, GetDocumentsDirectory());
        string finalMessage = BuildMessage(message, shareableAttachmentPath);
        Uri desktopUri = BuildDesktopUri(normalizedPhone, finalMessage);
        Uri webUri = BuildWebUri(normalizedPhone, finalMessage);

        bool opened = _launcher.TryOpen(desktopUri) || _launcher.TryOpen(webUri);
        if (!opened)
        {
            throw new InvalidOperationException("No se pudo abrir WhatsApp Desktop ni WhatsApp Web en este equipo.");
        }

        if (!string.IsNullOrWhiteSpace(shareableAttachmentPath))
        {
            _launcher.RevealFile(shareableAttachmentPath);
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

    public static string? PrepareAttachmentForManualAttach(string? attachmentPath, string documentsDirectory)
    {
        if (string.IsNullOrWhiteSpace(attachmentPath) || !File.Exists(attachmentPath))
        {
            return attachmentPath;
        }

        if (string.IsNullOrWhiteSpace(documentsDirectory))
        {
            return attachmentPath;
        }

        try
        {
            string targetDirectory = Path.Combine(documentsDirectory, "POS WhatsApp");
            Directory.CreateDirectory(targetDirectory);

            string targetPath = Path.Combine(targetDirectory, Path.GetFileName(attachmentPath));
            if (Path.GetFullPath(attachmentPath).Equals(Path.GetFullPath(targetPath), StringComparison.OrdinalIgnoreCase))
            {
                return targetPath;
            }

            File.Copy(attachmentPath, targetPath, overwrite: true);
            return targetPath;
        }
        catch (IOException)
        {
            return attachmentPath;
        }
        catch (UnauthorizedAccessException)
        {
            return attachmentPath;
        }
    }

    private string GetDocumentsDirectory()
    {
        try
        {
            return _documentsDirectoryProvider();
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    public static Uri BuildDesktopUri(string phoneNumber, string message) =>
        new($"whatsapp://send?phone={Uri.EscapeDataString(phoneNumber)}&text={Uri.EscapeDataString(message)}");

    public static Uri BuildWebUri(string phoneNumber, string message) =>
        new($"https://wa.me/{Uri.EscapeDataString(phoneNumber)}?text={Uri.EscapeDataString(message)}");
}
