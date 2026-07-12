using POS.Domain.Entities;

namespace POS.Domain.Interfaces;

public interface ISupplierQuotationRepository
{
    Task AddAsync(SupplierQuotation quotation, CancellationToken cancellationToken = default);
    Task<SupplierQuotation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
