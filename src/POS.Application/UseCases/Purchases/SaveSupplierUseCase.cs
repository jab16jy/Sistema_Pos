using POS.Application.DTOs.Purchases;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Purchases;

public sealed class SaveSupplierUseCase
{
    private readonly ISupplierRepository _suppliers;
    private readonly IUnitOfWork _unitOfWork;

    public SaveSupplierUseCase(ISupplierRepository suppliers, IUnitOfWork unitOfWork)
    {
        _suppliers = suppliers;
        _unitOfWork = unitOfWork;
    }

    public async Task<SupplierResult> ExecuteAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default)
    {
        Supplier supplier;
        if (request.Id is Guid supplierId)
        {
            supplier = await _suppliers.GetByIdAsync(supplierId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Supplier was not found.");
            supplier.UpdateContactInfo(request.Name, request.TaxId, request.Phone, request.Email);
            if (request.IsActive)
            {
                supplier.Activate();
            }
            else
            {
                supplier.Deactivate();
            }
            await _suppliers.UpdateAsync(supplier, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            supplier = new Supplier(request.Name, request.TaxId, request.Phone, request.Email);
            if (!request.IsActive)
            {
                supplier.Deactivate();
            }
            await _suppliers.AddAsync(supplier, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return SupplierResult.FromSupplier(supplier);
    }
}
