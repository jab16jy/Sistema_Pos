namespace POS.Application.Interfaces;

public interface IWhatsAppService
{
    Task<bool> SendAsync(
        string phoneNumber,
        string message,
        string? attachmentPath,
        CancellationToken ct = default);
}
