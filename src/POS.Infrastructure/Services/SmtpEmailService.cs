using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using POS.Application.Interfaces;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Services;

public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpRuntimeOptions _options;
    private readonly AppDbContext? _dbContext;

    public SmtpEmailService(SmtpRuntimeOptions options, AppDbContext? dbContext = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _dbContext = dbContext;
    }

    public async Task<bool> SendAsync(
        string to,
        string subject,
        string body,
        byte[] attachment,
        string attachmentName,
        CancellationToken ct = default)
    {
        ResolvedSmtpConfiguration configuration = await ResolveConfigurationAsync(ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(configuration.Host) ||
            string.IsNullOrWhiteSpace(configuration.FromAddress))
        {
            throw new InvalidOperationException("La configuración SMTP está incompleta. Configura servidor y remitente antes de enviar correos.");
        }

        try
        {
            using var client = new SmtpClient(configuration.Host, configuration.Port)
            {
                EnableSsl = configuration.UseSsl
            };

            if (!string.IsNullOrWhiteSpace(configuration.User))
            {
                client.Credentials = new NetworkCredential(configuration.User, configuration.Password ?? string.Empty);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(configuration.FromAddress),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            message.To.Add(to);

            using var stream = new MemoryStream(attachment);
            message.Attachments.Add(new Attachment(stream, attachmentName));

            await client.SendMailAsync(message, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (IsRecoverableSmtpFailure(ex))
        {
            throw new InvalidOperationException(
                "No se pudo enviar el correo. Verifica host, puerto, SSL/TLS, usuario, contraseña de aplicación y remitente SMTP.",
                ex);
        }
    }

    private async Task<ResolvedSmtpConfiguration> ResolveConfigurationAsync(CancellationToken ct)
    {
        var resolved = new ResolvedSmtpConfiguration(
            _options.Host,
            _options.Port,
            _options.User,
            _options.Password,
            _options.UseSsl,
            _options.FromAddress);

        if (_dbContext is null)
        {
            return resolved;
        }

        Domain.Entities.BusinessSettings? settings = await _dbContext.BusinessSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (settings is null)
        {
            return resolved;
        }

        bool usePersistedSmtp = !string.IsNullOrWhiteSpace(settings.SmtpHost)
            || !string.IsNullOrWhiteSpace(settings.SmtpSenderAddress)
            || !string.IsNullOrWhiteSpace(settings.SmtpUser)
            || !string.IsNullOrWhiteSpace(settings.SmtpPassword)
            || settings.SmtpPort.HasValue;

        if (!usePersistedSmtp)
        {
            return resolved;
        }

        return new ResolvedSmtpConfiguration(
            settings.SmtpHost,
            settings.SmtpPort ?? _options.Port,
            settings.SmtpUser,
            settings.SmtpPassword,
            settings.SmtpUseSsl,
            settings.SmtpSenderAddress);
    }

    private sealed record ResolvedSmtpConfiguration(
        string? Host,
        int Port,
        string? User,
        string? Password,
        bool UseSsl,
        string? FromAddress);

    private static bool IsRecoverableSmtpFailure(Exception ex) =>
        ex is SmtpException or SocketException or IOException or FormatException or InvalidOperationException;
}
