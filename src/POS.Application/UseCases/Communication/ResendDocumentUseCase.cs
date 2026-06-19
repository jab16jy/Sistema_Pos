using POS.Application.DTOs.Communication;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Communication;

public sealed class ResendDocumentUseCase
{
    private readonly IDocumentSendLogRepository _sendLogs;
    private readonly SendDocumentUseCase _sendDocument;

    public ResendDocumentUseCase(IDocumentSendLogRepository sendLogs, SendDocumentUseCase sendDocument)
    {
        _sendLogs = sendLogs;
        _sendDocument = sendDocument;
    }

    public async Task<SendDocumentResult> ExecuteAsync(Guid originalLogId, CancellationToken ct = default)
    {
        var log = await _sendLogs.GetByIdAsync(originalLogId, ct)
            ?? throw new InvalidOperationException($"Log de envío {originalLogId} no encontrado.");

        var request = new SendDocumentRequest(
            log.DocumentType,
            log.DocumentId,
            log.Channel,
            log.RecipientContact);

        return await _sendDocument.ExecuteAsync(request, ct);
    }
}
