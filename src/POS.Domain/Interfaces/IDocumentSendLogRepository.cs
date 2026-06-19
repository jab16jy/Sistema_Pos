using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface IDocumentSendLogRepository
{
    Task AddAsync(DocumentSendLog log, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentSendLog>> GetByDocumentAsync(Guid documentId, CancellationToken ct = default);
    Task<DocumentSendLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
