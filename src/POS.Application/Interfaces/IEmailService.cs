namespace POS.Application.Interfaces;

public interface IEmailService
{
    Task<bool> SendAsync(
        string to,
        string subject,
        string body,
        byte[] attachment,
        string attachmentName,
        CancellationToken ct = default);
}
