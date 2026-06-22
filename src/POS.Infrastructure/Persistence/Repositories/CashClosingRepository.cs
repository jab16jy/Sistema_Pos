using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF-backed repository for CashClosing snapshots.
/// </summary>
public sealed class CashClosingRepository : ICashClosingRepository
{
    private readonly AppDbContext _dbContext;

    public CashClosingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(CashClosing closing, CancellationToken cancellationToken = default)
    {
        await _dbContext.CashClosings.AddAsync(closing, cancellationToken);
    }
}
