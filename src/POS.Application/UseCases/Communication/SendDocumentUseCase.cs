using POS.Application.DTOs.Communication;
using POS.Application.Interfaces;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Communication;

public sealed class SendDocumentUseCase
{
    private readonly IPdfService _pdfService;
    private readonly IEmailService _emailService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly IDocumentSendLogRepository _sendLogs;
    private readonly IUnitOfWork _unitOfWork;

    public SendDocumentUseCase(
        IPdfService pdfService,
        IEmailService emailService,
        IWhatsAppService whatsAppService,
        IDocumentSendLogRepository sendLogs,
        IUnitOfWork unitOfWork)
    {
        _pdfService = pdfService;
        _emailService = emailService;
        _whatsAppService = whatsAppService;
        _sendLogs = sendLogs;
        _unitOfWork = unitOfWork;
    }

    public async Task<SendDocumentResult> ExecuteAsync(SendDocumentRequest request, CancellationToken ct = default)
    {
        var pdfBytes = request.DocumentType == DocumentType.Quote
            ? await _pdfService.GenerateQuotePdfAsync(request.DocumentId, ct)
            : await _pdfService.GenerateInvoicePdfAsync(request.DocumentId, ct);

        var docTypeName = request.DocumentType == DocumentType.Quote ? "Cotizacion" : "Factura";

        bool success;
        string? errorMessage = null;

        try
        {
            if (request.Channel == SendChannel.Email)
            {
                var subject = $"{docTypeName} {request.DocumentId}";
                const string body = "Adjunto encontrará el documento solicitado.";

                success = await _emailService.SendAsync(
                    request.RecipientContact,
                    subject,
                    body,
                    pdfBytes,
                    $"{docTypeName}.pdf",
                    ct);
            }
            else
            {
                var tempPath = Path.Combine(Path.GetTempPath(), $"{docTypeName}_{request.DocumentId:N}.pdf");
                await File.WriteAllBytesAsync(tempPath, pdfBytes, ct);

                var message = $"Adjunto encontrará su {docTypeName.ToLower()}.";
                success = await _whatsAppService.SendAsync(request.RecipientContact, message, tempPath, ct);
            }
        }
        catch (Exception ex)
        {
            success = false;
            errorMessage = ex.Message;
        }

        if (!success && string.IsNullOrWhiteSpace(errorMessage))
        {
            errorMessage = "El servicio de envío no confirmó la entrega del documento.";
        }

        var log = new DocumentSendLog(
            request.DocumentType,
            request.DocumentId,
            request.Channel,
            request.RecipientContact,
            success,
            errorMessage);

        await _sendLogs.AddAsync(log, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return new SendDocumentResult(success, errorMessage, log.SentAt);
    }
}
