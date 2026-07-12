using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class Purchase
{
    private readonly List<PurchaseItem> _items = [];

    private Purchase() { }

    public Purchase(
        Guid supplierId,
        Guid userId,
        DateOnly effectiveDate,
        string? invoiceNumber,
        string? paymentTerms,
        string paymentMethod,
        string? observations,
        decimal vatRate)
    {
        if (supplierId == Guid.Empty) throw new ArgumentException("Supplier is required.", nameof(supplierId));
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(paymentMethod)) throw new ArgumentException("Payment method is required.", nameof(paymentMethod));
        if (vatRate < 0m) throw new ArgumentOutOfRangeException(nameof(vatRate));

        Id = Guid.NewGuid();
        SupplierId = supplierId;
        UserId = userId;
        CreatedAt = DateTimeOffset.UtcNow;
        EffectiveDate = effectiveDate;
        InvoiceNumber = Normalize(invoiceNumber) ?? GenerateInvoiceNumber(Id);
        PaymentTerms = Normalize(paymentTerms);
        PaymentMethod = paymentMethod.Trim();
        Observations = Normalize(observations);
        VatPolicy = PurchaseVatPolicy.Additional;
        VatRate = vatRate;
        RecalculateTotals();
    }

    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateOnly? EffectiveDate { get; private set; }
    public string? InvoiceNumber { get; private set; }
    public string? PaymentTerms { get; private set; }
    public string? PaymentMethod { get; private set; }
    public string? Observations { get; private set; }
    public PurchaseVatPolicy? VatPolicy { get; private set; }
    public decimal? VatRate { get; private set; }
    public Money? Subtotal { get; private set; }
    public Money? VatAmount { get; private set; }
    public Money? Total { get; private set; }
    public IReadOnlyCollection<PurchaseItem> Items => _items.AsReadOnly();

    public void AddItem(PurchaseItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        Money subtotal = _items.Aggregate(Money.Zero, (current, item) => current + (item.Subtotal ?? Money.Zero));
        Subtotal = subtotal;
        VatAmount = new Money(subtotal.Amount * (VatRate ?? 0m) / 100m);
        Total = subtotal + VatAmount.Value;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GenerateInvoiceNumber(Guid purchaseId)
        => $"CMP-{Convert.ToBase64String(purchaseId.ToByteArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
}
