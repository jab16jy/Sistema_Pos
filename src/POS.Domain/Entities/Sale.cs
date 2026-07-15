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
        => AddItem(product, quantity, ProductPresentation.Unit, discount);

    public void AddItem(Product product, Quantity quantity, ProductPresentation presentation, Money discount)
    {
        ArgumentNullException.ThrowIfNull(product);
        int unitsPerPresentation = presentation == ProductPresentation.Box
            ? product.UnitsPerBox ?? throw new InvalidOperationException($"Product '{product.Name}' is not configured for box sales.")
            : 1;

        AddItem(product, quantity, presentation, discount, product.PriceFor(presentation), unitsPerPresentation);
    }

    /// <summary>
    /// Adds a line with immutable commercial snapshots. Quote conversion uses this overload so
    /// later catalogue price or box-equivalence changes never rewrite the agreed quote.
    /// </summary>
    public void AddItem(
        Product product,
        Quantity quantity,
        ProductPresentation presentation,
        Money discount,
        Money unitPrice,
        int unitsPerPresentation)
    {
        if (Status != SaleStatus.Draft)
        {
            throw new InvalidOperationException("Only draft sales can be modified.");
        }

        ArgumentNullException.ThrowIfNull(product);
        int factor = presentation == ProductPresentation.Unit
            ? 1
            : unitsPerPresentation >= 2
                ? unitsPerPresentation
                : throw new ArgumentOutOfRangeException(nameof(unitsPerPresentation), "A box presentation must contain at least 2 base units.");

        // Stock remains in base units even when a historical quote uses an old box equivalence.
        product.DecreaseStock(new Quantity(quantity.Value * factor));

        var unitCost = new Money((product.CostPrice?.Amount ?? 0m) * factor);
        _items.Add(new SaleItem(product.Id, product.Name, quantity, unitPrice, discount, unitCost, product.TaxRate, presentation, factor));
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

    public Money LoyaltyPointsTenderTotal() => new(_payments
        .Where(payment => payment.Method == PaymentMethod.LoyaltyPoints)
        .Sum(payment => payment.Amount.Amount));

    public Money EffectiveEarnableTotal() => new(Math.Max(0m, Total().Amount - LoyaltyPointsTenderTotal().Amount));

    public Money TaxTotal() => new(_items.Sum(item => item.TaxAmount().Amount));

    public Money TaxableBaseTotal() => new(_items.Sum(item => item.TaxableBaseAmount().Amount));

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

    public void Resume()
    {
        if (Status != SaleStatus.OnHold)
        {
            throw new InvalidOperationException("Only held sales can be resumed.");
        }

        Status = SaleStatus.Draft;
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

    public void MarkCancelled()
    {
        if (Status == SaleStatus.Cancelled)
        {
            throw new InvalidOperationException("Sale already cancelled.");
        }

        if (Status == SaleStatus.Returned)
        {
            throw new InvalidOperationException("Cannot cancel a returned sale.");
        }

        if (Status != SaleStatus.Completed)
        {
            throw new InvalidOperationException("Only completed sales can be cancelled.");
        }

        Status = SaleStatus.Cancelled;
    }
}
