using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class SaleReturnRepository : ISaleReturnRepository
{
    private readonly AppDbContext _dbContext;

    public SaleReturnRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(SaleReturn saleReturn, CancellationToken cancellationToken = default)
    {
        await _dbContext.SaleReturns.AddAsync(saleReturn, cancellationToken).ConfigureAwait(false);
    }

    public Task<SaleReturn?> GetBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        return _dbContext.SaleReturns
            .AsNoTracking()
            .Include(r => r.Items)
            .FirstOrDefaultAsync(saleReturn => saleReturn.OriginalSaleId == saleId, cancellationToken);
    }

    public async Task<IReadOnlyList<SaleReturn>> GetAllBySaleIdAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var list = await _dbContext.SaleReturns
            .AsNoTracking()
            .Include(r => r.Items)
            .Where(saleReturn => saleReturn.OriginalSaleId == saleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return list;
    }
}
