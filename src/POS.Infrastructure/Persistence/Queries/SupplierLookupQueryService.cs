using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Persistence.Queries;

public sealed class SupplierLookupQueryService : ISupplierLookupQueryService
{
    private readonly AppDbContext _dbContext;

    public SupplierLookupQueryService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SupplierSearchResult>> SearchAsync(SupplierSearchRequest request, CancellationToken cancellationToken = default)
    {
        IQueryable<Domain.Entities.Supplier> query = _dbContext.Suppliers.AsNoTracking();

        if (!request.IncludeInactive)
        {
            query = query.Where(supplier => supplier.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            string normalized = request.SearchText.Trim();
            query = query.Where(supplier =>
                supplier.Name.Contains(normalized) ||
                supplier.TaxId.Contains(normalized) ||
                (supplier.Phone != null && supplier.Phone.Contains(normalized)) ||
                (supplier.Email != null && supplier.Email.Contains(normalized)));
        }

        return await query
            .OrderBy(supplier => supplier.Name)
            .Take(50)
            .Select(supplier => new SupplierSearchResult(supplier.Id, supplier.Name, supplier.TaxId, supplier.Phone, supplier.Email, supplier.IsActive))
            .ToListAsync(cancellationToken);
    }
}
