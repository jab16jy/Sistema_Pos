using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Services;

public sealed class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public SmtpEmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<bool> SendAsync(
        string to,
        string subject,
        string body,
        byte[] attachment,
        string attachmentName,
        CancellationToken ct = default)
    {
        var host = _configuration["Smtp:Host"] ?? "localhost";
        var port = int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 587;
        var user = _configuration["Smtp:User"] ?? string.Empty;
        var password = _configuration["Smtp:Password"] ?? string.Empty;
        var useSsl = bool.TryParse(_configuration["Smtp:UseSsl"], out var ssl) && ssl;

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, password),
            EnableSsl = useSsl
        };

        using var message = new MailMessage
        {
            From = new MailAddress(user),
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
