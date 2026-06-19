using POS.Domain.Enums;

namespace POS.Domain.Entities;

public sealed class DocumentSendLog
{
    private DocumentSendLog()
    {
        RecipientContact = string.Empty;
    }

    public DocumentSendLog(DocumentType documentType, Guid documentId, SendChannel channel, string recipientContact, bool success, string? errorMessage = null)
    {
        Id = Guid.NewGuid();
        DocumentType = documentType;
        DocumentId = documentId;
        Channel = channel;
        RecipientContact = recipientContact;
        Success = success;
        ErrorMessage = errorMessage;
        SentAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public DocumentType DocumentType { get; private set; }
    public Guid DocumentId { get; private set; }
    public SendChannel Channel { get; private set; }
    public string RecipientContact { get; private set; }
    public DateTime SentAt { get; private set; }
    public bool Success { get; private set; }
    public string? ErrorMessage { get; private set; }
}
