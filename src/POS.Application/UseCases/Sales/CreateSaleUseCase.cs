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
    private readonly IReceivableRepository? _receivables;

    /// <summary>Legacy constructor — no receivable support. Used by tests that don't need deferred balance.</summary>
    public CreateSaleUseCase(IProductRepository products, ISaleRepository sales, IUnitOfWork unitOfWork)
        : this(products, sales, unitOfWork, null)
    {
    }

    public CreateSaleUseCase(
        IProductRepository products,
        ISaleRepository sales,
        IUnitOfWork unitOfWork,
        IReceivableRepository? receivables)
    {
        _products = products;
        _sales = sales;
        _unitOfWork = unitOfWork;
        _receivables = receivables;
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

        if (request.SettlementPlan is not null)
        {
            // Settlement plan path: collected + deferred
            var plan = request.SettlementPlan;

            // Guard: deferred balance requires an identified customer
            if (plan.DeferredBalanceAmount > 0m && request.CustomerId is null)
            {
                throw new InvalidOperationException(
                    "A customer must be identified to create a deferred balance.");
            }

            sale.SetSettlementPlan(plan);

            // Only add a payment line for the collected portion (deferred is a receivable, not a payment)
            if (plan.CollectedAmount > 0m && plan.CollectedMethod is not null)
            {
                sale.AddPayment(new Payment(
                    plan.CollectedMethod.Value,
                    new Money(plan.CollectedAmount),
                    plan.CollectedMethod == Domain.Enums.PaymentMethod.Cash ? new Money(plan.CollectedAmount) : null,
                    null));
            }
        }
        else
        {
            // Legacy full-payment path
            foreach (var payment in request.Payments!)
            {
                sale.AddPayment(new Payment(
                    payment.Method,
                    new Money(payment.Amount),
                    payment.ReceivedAmount is null ? null : new Money(payment.ReceivedAmount.Value),
                    payment.BankOrPlatform));
            }
        }

        sale.Complete();

        await _sales.AddAsync(sale, cancellationToken);

        // Create receivable inside the same transaction if a deferred balance exists
        Receivable? receivable = null;
        if (sale.DeferredBalance().Amount > 0m && request.CustomerId.HasValue)
        {
            receivable = new Receivable(request.CustomerId.Value, sale.Id, sale.DeferredBalance());
            if (_receivables is not null)
            {
                await _receivables.AddAsync(receivable, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateSaleResult(
            sale.Id,
            sale.InvoiceNumber,
            sale.Total().Amount,
            sale.CashInDrawer().Amount,
            sale.DeferredBalance().Amount);
    }
}
