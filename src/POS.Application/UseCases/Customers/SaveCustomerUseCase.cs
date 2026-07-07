using POS.Application.DTOs.Customers;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Customers;

public sealed class SaveCustomerUseCase
{
    private readonly ICustomerRepository _customers;
    private readonly IUnitOfWork _unitOfWork;

    public SaveCustomerUseCase(ICustomerRepository customers, IUnitOfWork unitOfWork)
    {
        _customers = customers;
        _unitOfWork = unitOfWork;
    }

    public async Task<CustomerResult> ExecuteAsync(SaveCustomerRequest request, CancellationToken cancellationToken = default)
    {
        // Cédula/identificación must be unique across customers. On edit, allow the record to keep its own.
        if (!string.IsNullOrWhiteSpace(request.Identification))
        {
            var duplicate = await _customers.GetByIdentificationAsync(request.Identification.Trim(), cancellationToken);
            if (duplicate is not null && duplicate.Id != request.CustomerId)
            {
                throw new InvalidOperationException($"Ya existe un cliente con la identificación '{request.Identification.Trim()}'.");
            }
        }

        Customer customer;
        if (request.CustomerId.HasValue)
        {
            customer = await _customers.GetByIdAsync(request.CustomerId.Value, cancellationToken)
                ?? throw new InvalidOperationException($"Customer '{request.CustomerId.Value}' was not found.");
            customer.UpdateContactInfo(request.FullName, request.Identification, request.Phone, request.Email, request.Address);
        }
        else
        {
            customer = new Customer(request.FullName, request.Identification);
            customer.UpdateContactInfo(request.FullName, request.Identification, request.Phone, request.Email, request.Address);
            await _customers.AddAsync(customer, cancellationToken);
        }

        if (request.IsActive)
        {
            customer.Reactivate();
        }
        else
        {
            customer.Deactivate();
        }

        if (request.CustomerId.HasValue)
        {
            await _customers.UpdateAsync(customer, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return CustomerResult.FromCustomer(customer);
    }
}
