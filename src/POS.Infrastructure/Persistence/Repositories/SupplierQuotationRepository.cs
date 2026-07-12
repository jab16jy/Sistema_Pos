using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Persistence.Repositories;

public sealed class SupplierQuotationRepository : ISupplierQuotationRepository
{
    private readonly AppDbContext _dbContext;
    public SupplierQuotationRepository(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(SupplierQuotation quotation, CancellationToken cancellationToken = default)
    {
        await _dbContext.SupplierQuotations.AddAsync(quotation, cancellationToken).ConfigureAwait(false);
    }

    public Task<SupplierQuotation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.SupplierQuotations.Include(quotation => quotation.Items).FirstOrDefaultAsync(quotation => quotation.Id == id, cancellationToken);
}
