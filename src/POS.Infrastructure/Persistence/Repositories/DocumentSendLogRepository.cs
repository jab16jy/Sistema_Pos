using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class DocumentSendLogRepository : IDocumentSendLogRepository
{
    private readonly AppDbContext _dbContext;

    public DocumentSendLogRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(DocumentSendLog log, CancellationToken ct = default)
    {
        await _dbContext.DocumentSendLogs.AddAsync(log, ct);
    }

    public async Task<IReadOnlyList<DocumentSendLog>> GetByDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        return await _dbContext.DocumentSendLogs
            .Where(l => l.DocumentId == documentId)
            .ToListAsync(ct);
    }

    public Task<DocumentSendLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _dbContext.DocumentSendLogs
            .FirstOrDefaultAsync(l => l.Id == id, ct);
    }
}
