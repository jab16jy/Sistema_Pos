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
