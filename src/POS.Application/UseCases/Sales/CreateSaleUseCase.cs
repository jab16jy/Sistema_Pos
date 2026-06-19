using POS.Application.DTOs.Sales;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Sales;

public sealed class CreateSaleUseCase
{
    private readonly IProductRepository _products;
    private readonly ISaleRepository _sales;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSaleUseCase(IProductRepository products, ISaleRepository sales, IUnitOfWork unitOfWork)
    {
        _products = products;
        _sales = sales;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateSaleResult> ExecuteAsync(CreateSaleRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("Add at least one product before completing the sale.");
        }

        var sale = new Sale(request.CashierUserId, request.CustomerId);

        foreach (var item in request.Items)
        {
            var product = await _products.GetByIdAsync(item.ProductId, cancellationToken)
                ?? throw new InvalidOperationException($"Product '{item.ProductId}' was not found.");

            sale.AddItem(product, new Quantity(item.Quantity), new Money(item.Discount));
        }

        foreach (var payment in request.Payments)
        {
            sale.AddPayment(new Payment(
                payment.Method,
                new Money(payment.Amount),
                payment.ReceivedAmount is null ? null : new Money(payment.ReceivedAmount.Value),
                payment.BankOrPlatform));
        }

        sale.Complete();

        await _sales.AddAsync(sale, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateSaleResult(sale.Id, sale.InvoiceNumber, sale.Total().Amount, sale.CashInDrawer().Amount);
    }
}
