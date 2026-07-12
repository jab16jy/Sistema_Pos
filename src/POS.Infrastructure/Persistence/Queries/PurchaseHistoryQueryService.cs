using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs.Purchases;
using POS.Application.Interfaces;

namespace POS.Infrastructure.Persistence.Queries;

public sealed class PurchaseHistoryQueryService : IPurchaseHistoryQueryService
{
    private readonly AppDbContext _dbContext;
    public PurchaseHistoryQueryService(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<PagedResult<PurchaseHistoryResult>> SearchAsync(PurchaseHistoryRequest request, CancellationToken cancellationToken = default)
    {
        IQueryable<Domain.Entities.Purchase> query = _dbContext.Purchases.AsNoTracking().Include(purchase => purchase.Items);
        if (request.SupplierId is Guid supplierId) query = query.Where(purchase => purchase.SupplierId == supplierId);
        if (request.FromEffectiveDate is DateOnly from) query = query.Where(purchase => purchase.EffectiveDate >= from);
        if (request.ToEffectiveDate is DateOnly to) query = query.Where(purchase => purchase.EffectiveDate <= to);
        if (!string.IsNullOrWhiteSpace(request.InvoiceNumber))
        {
            string invoice = request.InvoiceNumber.Trim();
            query = query.Where(purchase => purchase.InvoiceNumber != null && purchase.InvoiceNumber.Contains(invoice));
        }

        int total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var page = await (from purchase in query
                          join supplier in _dbContext.Suppliers.AsNoTracking() on purchase.SupplierId equals supplier.Id
                          orderby purchase.EffectiveDate descending, purchase.CreatedAt descending
                          select new { purchase, supplier.Name })
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResult<PurchaseHistoryResult>(page.Select(row => new PurchaseHistoryResult(
            row.purchase.Id, row.purchase.SupplierId, row.Name, row.purchase.EffectiveDate, row.purchase.InvoiceNumber, row.purchase.PaymentTerms, row.purchase.PaymentMethod, row.purchase.Observations,
            row.purchase.Subtotal?.Amount, row.purchase.VatRate, row.purchase.VatAmount?.Amount, row.purchase.Total?.Amount,
            row.purchase.Items.Select(item => new PurchaseHistoryLineResult(item.ProductId, item.ProductName, item.ProductReference, item.PurchaseUnitName, item.Quantity, item.UnitsPerPurchaseUnit, item.InventoryUnitsReceived, item.PurchaseUnitCost?.Amount, item.BaseUnitCost?.Amount, item.Subtotal?.Amount, item.TotalCost)).ToList())).ToList(), total, request.Page, request.PageSize);
    }
}
