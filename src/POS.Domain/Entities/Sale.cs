using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class Sale
{
    private readonly List<SaleItem> _items = [];
    private readonly List<Payment> _payments = [];
    private SaleSettlementPlan? _settlementPlan;

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

    /// <summary>
    /// Attaches a settlement plan to the sale, describing how much is collected
    /// immediately versus deferred as a receivable. Must be set before calling Complete.
    /// </summary>
    public void SetSettlementPlan(SaleSettlementPlan plan)
    {
        if (Status != SaleStatus.Draft)
        {
            throw new InvalidOperationException("Settlement plan can only be set on draft sales.");
        }

        _settlementPlan = plan ?? throw new ArgumentNullException(nameof(plan));
    }

    /// <summary>Returns the deferred balance from the settlement plan, or zero if no plan.</summary>
    public Money DeferredBalance() =>
        _settlementPlan is { DeferredBalanceAmount: > 0m } plan
            ? new Money(plan.DeferredBalanceAmount)
            : Money.Zero;

    public void AddItem(Product product, Quantity quantity, Money discount)
    {
        if (Status != SaleStatus.Draft)
        {
            throw new InvalidOperationException("Only draft sales can be modified.");
        }

        product.DecreaseStock(quantity);
        _items.Add(new SaleItem(product.Id, product.Name, quantity, product.SalePrice, discount, product.CostPrice));
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

        var total = Total().Amount;
        var deferred = DeferredBalance().Amount;
        var covered = TotalPaid().Amount + deferred;

        if (covered < total)
        {
            throw new InvalidOperationException("The received payment does not cover the sale total.");
        }

        // Spec: deferred balance without identified customer is rejected (orphan credit guard)
        if (deferred > 0m && CustomerId is null)
        {
            throw new InvalidOperationException("A customer must be identified to create a deferred balance.");
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

    public void MarkReturned()
    {
        if (Status == SaleStatus.Returned)
        {
            throw new InvalidOperationException("Sale already returned.");
        }

        if (Status == SaleStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot return a cancelled sale.");
        }

        if (Status != SaleStatus.Completed)
        {
            throw new InvalidOperationException("Only completed sales can be returned.");
        }

        Status = SaleStatus.Returned;
    }
}
