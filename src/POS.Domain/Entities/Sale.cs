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

        if (plan.DeferredBalanceAmount < 0m)
        {
            throw new InvalidOperationException("Settlement amounts cannot be negative.");
        }

        foreach (SettlementCollectedPayment leg in plan.CollectedPayments)
        {
            if (leg.Amount <= 0m)
            {
                throw new InvalidOperationException("A collected payment leg must have a positive amount.");
            }

            if (leg.Method == PaymentMethod.LoyaltyPoints)
            {
                throw new InvalidOperationException("Loyalty points cannot be used in a deferred settlement plan.");
            }
        }
    }

    /// <summary>Returns the deferred balance from the settlement plan, or zero if no plan.</summary>
    public Money DeferredBalance() =>
        _settlementPlan is { DeferredBalanceAmount: > 0m } plan
            ? new Money(plan.DeferredBalanceAmount)
            : Money.Zero;

    public void AddItem(Product product, Quantity quantity, Money discount)
        => AddItem(product, quantity, null, discount);

    public void AddItem(Product product, Quantity quantity, string? presentationName, Money discount)
    {
        ArgumentNullException.ThrowIfNull(product);
        AddItem(product, quantity, presentationName, discount, product.PriceFor(presentationName), product.UnitsFor(presentationName));
    }

    /// <summary>
    /// Adds a line with immutable commercial snapshots. Quote conversion uses this overload so
    /// later catalogue price or pack-equivalence changes never rewrite the agreed quote.
    /// </summary>
    public void AddItem(
        Product product,
        Quantity quantity,
        string? presentationName,
        Money discount,
        Money unitPrice,
        int unitsPerPresentation)
    {
        if (Status != SaleStatus.Draft)
        {
            throw new InvalidOperationException("Only draft sales can be modified.");
        }

        ArgumentNullException.ThrowIfNull(product);
        string? name = string.IsNullOrWhiteSpace(presentationName) ? null : presentationName.Trim();
        int factor = name is null
            ? 1
            : unitsPerPresentation >= 1
                ? unitsPerPresentation
                : throw new ArgumentOutOfRangeException(nameof(unitsPerPresentation), "A packaged presentation must contain at least 1 base unit.");

        // Stock remains in base units even when a historical quote uses an old pack equivalence.
        product.DecreaseStock(new Quantity(quantity.Value * factor));

        var unitCost = new Money((product.CostPrice?.Amount ?? 0m) * factor);
        _items.Add(new SaleItem(product.Id, product.Name, quantity, unitPrice, discount, unitCost, product.TaxRate, name, factor));
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

    public Money EffectiveEarnableTotal() => new(Math.Max(0m, Total().Amount - LoyaltyPointsTenderTotal().Amount - DeferredBalance().Amount));

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

        if (covered != total)
        {
            throw new InvalidOperationException("The received payment and deferred balance must equal the sale total.");
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

        // ponytail: held sales can also be abandoned (stock restored by caller).
        if (Status != SaleStatus.Completed && Status != SaleStatus.OnHold)
        {
            throw new InvalidOperationException("Only completed or held sales can be cancelled.");
        }

        Status = SaleStatus.Cancelled;
    }
}
