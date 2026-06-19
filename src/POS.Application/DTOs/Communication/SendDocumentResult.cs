namespace POS.Application.DTOs.Communication;

public sealed record SendDocumentResult(
    bool Success,
    string? ErrorMessage,
    DateTime SentAt);
