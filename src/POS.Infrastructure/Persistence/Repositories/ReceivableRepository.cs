using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class ReceivableRepository : IReceivableRepository
{
    private readonly AppDbContext _dbContext;

    public ReceivableRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Receivable receivable, CancellationToken cancellationToken = default)
    {
        await _dbContext.Receivables.AddAsync(receivable, cancellationToken);
    }

    public async Task<Receivable?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Receivables
            .Include(r => r.Settlements)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task UpdateAsync(Receivable receivable, CancellationToken cancellationToken = default)
    {
        _dbContext.Receivables.Update(receivable);
        return Task.CompletedTask;
    }
}
