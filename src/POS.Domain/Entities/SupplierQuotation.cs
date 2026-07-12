using POS.Domain.Enums;

namespace POS.Domain.Entities;

public sealed class SupplierQuotation
{
    private readonly List<SupplierQuotationItem> _items = [];

    private SupplierQuotation() { }

    public SupplierQuotation(Guid supplierId, DateOnly effectiveDate, string? paymentTerms, string? observations)
    {
        if (supplierId == Guid.Empty) throw new ArgumentException("Supplier is required.", nameof(supplierId));
        Id = Guid.NewGuid();
        SupplierId = supplierId;
        EffectiveDate = effectiveDate;
        PaymentTerms = string.IsNullOrWhiteSpace(paymentTerms) ? null : paymentTerms.Trim();
        Observations = string.IsNullOrWhiteSpace(observations) ? null : observations.Trim();
        Status = SupplierQuotationStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public string? PaymentTerms { get; private set; }
    public string? Observations { get; private set; }
    public SupplierQuotationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<SupplierQuotationItem> Items => _items.AsReadOnly();

    public void AddItem(SupplierQuotationItem item)
    {
        if (Status != SupplierQuotationStatus.Draft) throw new InvalidOperationException("Only draft supplier quotations can be changed.");
        _items.Add(item ?? throw new ArgumentNullException(nameof(item)));
    }

    public void Accept()
    {
        if (_items.Count == 0) throw new InvalidOperationException("A supplier quotation must contain at least one item.");
        if (Status != SupplierQuotationStatus.Draft) throw new InvalidOperationException("Only a draft supplier quotation can be accepted.");
        Status = SupplierQuotationStatus.Accepted;
    }

    public void Expire() => Status = SupplierQuotationStatus.Expired;
    public void Reject() => Status = SupplierQuotationStatus.Rejected;
}
