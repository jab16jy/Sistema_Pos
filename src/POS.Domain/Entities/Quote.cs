using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class Quote
{
    private readonly List<QuoteItem> _items = new();

    private Quote()
    {
        QuoteNumber = string.Empty;
        CustomerName = string.Empty;
    }

    public Quote(string quoteNumber, string customerName, int validityDays, Guid? customerId = null, string? customerPhone = null, string? customerEmail = null, string? observations = null)
    {
        if (string.IsNullOrWhiteSpace(quoteNumber))
        {
            throw new ArgumentException("Quote number is required.", nameof(quoteNumber));
        }

        if (validityDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(validityDays), "Validity days must be greater than zero.");
        }

        Id = Guid.NewGuid();
        QuoteNumber = quoteNumber.Trim();
        CustomerName = ContactInfo.NormalizeName(customerName);
        ValidityDays = validityDays;
        CustomerId = customerId;
        CustomerPhone = ContactInfo.NormalizePhone(customerPhone);
        CustomerEmail = ContactInfo.NormalizeEmail(customerEmail);
        Observations = string.IsNullOrWhiteSpace(observations) ? null : observations.Trim();
        Status = QuoteStatus.Draft;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = CreatedAt.AddDays(validityDays);
    }

    public Guid Id { get; private set; }
    public string QuoteNumber { get; private set; }
    public Guid? CustomerId { get; private set; }
    public string CustomerName { get; private set; }
    public string? CustomerPhone { get; private set; }
    public string? CustomerEmail { get; private set; }
    public string? Observations { get; private set; }
    public int ValidityDays { get; private set; }
    public QuoteStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public Guid? ConvertedSaleId { get; private set; }
    public IReadOnlyCollection<QuoteItem> Items => _items.AsReadOnly();

    public decimal Total => _items.Sum(i => i.Subtotal().Amount);

    public void AddItem(QuoteItem item)
    {
        if (Status == QuoteStatus.Converted)
        {
            throw new QuoteAlreadyConvertedException(QuoteNumber);
        }

        _items.Add(item);
    }

    public void Convert(Guid saleId)
    {
        if (Status == QuoteStatus.Converted)
        {
            throw new QuoteAlreadyConvertedException(QuoteNumber);
        }

        Status = QuoteStatus.Converted;
        ConvertedSaleId = saleId;
    }

    public void Expire()
    {
        if (Status is QuoteStatus.Draft or QuoteStatus.Active)
        {
            Status = QuoteStatus.Expired;
        }
    }
}
