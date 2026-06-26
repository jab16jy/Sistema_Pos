using System.Net;
using System.Net.Mail;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

public sealed class SmtpEmailService : IEmailService
{
    private readonly SmtpRuntimeOptions _options;

    public SmtpEmailService(SmtpRuntimeOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<bool> SendAsync(
        string to,
        string subject,
        string body,
        byte[] attachment,
        string attachmentName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            throw new InvalidOperationException("La configuración SMTP está incompleta. Configura servidor y remitente antes de enviar correos.");
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.User))
        {
            client.Credentials = new NetworkCredential(_options.User, _options.Password ?? string.Empty);
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        message.To.Add(to);

        using var stream = new MemoryStream(attachment);
        message.Attachments.Add(new Attachment(stream, attachmentName));

        await client.SendMailAsync(message, ct);
        return true;
    }
}
