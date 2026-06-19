using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class Sale
{
    private readonly List<SaleItem> _items = [];
    private readonly List<Payment> _payments = [];

    private Sale()
    {
        InvoiceNumber = string.Empty;
    }

    public Sale(Guid cashierUserId, Guid? customerId = null)
    {
        Id = Guid.NewGuid();
        CashierUserId = cashierUserId;
        CustomerId = customerId;
        InvoiceNumber = $"POS-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        Status = SaleStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string InvoiceNumber { get; private set; }
    public Guid CashierUserId { get; private set; }
    public Guid? CustomerId { get; private set; }
    public SaleStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();

    public void AddItem(Product product, Quantity quantity, Money discount)
    {
        if (Status != SaleStatus.Draft)
        {
            throw new InvalidOperationException("Only draft sales can be modified.");
        }

        product.DecreaseStock(quantity);
        _items.Add(new SaleItem(product.Id, product.Name, quantity, product.SalePrice, discount));
    }

    public void AddPayment(Payment payment)
    {
        if (Status != SaleStatus.Draft)
        {
            throw new InvalidOperationException("Only draft sales can be paid.");
        }

        _payments.Add(payment);
    }

    public Money Total() => new(_items.Sum(item => item.Subtotal().Amount));

    public Money TotalPaid() => new(_payments.Sum(payment => payment.Amount.Amount));

    public Money CashInDrawer() => new(_payments.Where(payment => payment.Method == PaymentMethod.Cash).Sum(payment => payment.Amount.Amount));

    public void Complete()
    {
        if (_items.Count == 0)
        {
            throw new InvalidOperationException("Add at least one product before completing the sale.");
        }

        if (TotalPaid().Amount < Total().Amount)
        {
            throw new InvalidOperationException("The received payment does not cover the sale total.");
        }

        Status = SaleStatus.Completed;
    }

    public void Hold()
    {
        if (Status != SaleStatus.Draft)
        {
            throw new InvalidOperationException("Only draft sales can be held.");
        }

        Status = SaleStatus.OnHold;
    }
}
