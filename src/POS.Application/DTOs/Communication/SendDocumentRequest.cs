using POS.Domain.Enums;

namespace POS.Application.DTOs.Communication;

public sealed record SendDocumentRequest(
    DocumentType DocumentType,
    Guid DocumentId,
    SendChannel Channel,
    string RecipientContact);
