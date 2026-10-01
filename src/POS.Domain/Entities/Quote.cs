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

    /// <summary>
    /// A quote stays editable only while it is still a proposal. Converted is the point where it
    /// became a Sale, so that plus Cancelled and Expired are frozen for good.
    /// </summary>
    public bool CanEdit => Status is QuoteStatus.Draft or QuoteStatus.Active;

    public void AddItem(QuoteItem item)
    {
        if (Status == QuoteStatus.Converted)
        {
            throw new QuoteAlreadyConvertedException(QuoteNumber);
        }

        _items.Add(item);
    }

    /// <summary>
    /// Corrects the header of a quote that is still a proposal. QuoteNumber, Id and CreatedAt are
    /// deliberately out of reach: the number is part of what the client was quoted, so an unlimited
    /// number of corrections still has to describe the same proposal.
    /// </summary>
    public void Update(string customerName, int validityDays, string? phone, string? email, string? observations)
    {
        EnsureEditable();

        if (validityDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(validityDays), "Validity days must be greater than zero.");
        }

        CustomerName = ContactInfo.NormalizeName(customerName);
        ValidityDays = validityDays;
        CustomerPhone = ContactInfo.NormalizePhone(phone);
        CustomerEmail = ContactInfo.NormalizeEmail(email);
        Observations = string.IsNullOrWhiteSpace(observations) ? null : observations.Trim();
        // Anchored to CreatedAt, never to DateTime.UtcNow: a validity window is part of what was
        // offered, so editing it slides the deadline instead of silently restarting the countdown
        // and re-validating an offer that had already lapsed.
        ExpiresAt = CreatedAt.AddDays(validityDays);
    }

    /// <summary>
    /// Swaps the whole line collection. QuoteItem is immutable, so a corrected quantity or price is
    /// a different item rather than a mutated one — which also keeps the discount range check in the
    /// QuoteItem constructor and lets the repository tell a dropped line from a corrected one by Id.
    /// </summary>
    public void ReplaceItems(IEnumerable<QuoteItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        EnsureEditable();

        _items.Clear();
        _items.AddRange(items);
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

    public void Cancel()
    {
        if (Status == QuoteStatus.Converted)
        {
            throw new InvalidOperationException($"La cotización '{QuoteNumber}' ya fue convertida a venta y no se puede anular.");
        }

        if (Status == QuoteStatus.Cancelled)
        {
            return;
        }

        Status = QuoteStatus.Cancelled;
    }

    private void EnsureEditable()
    {
        if (CanEdit)
        {
            return;
        }

        if (Status == QuoteStatus.Converted)
        {
            throw new QuoteAlreadyConvertedException(QuoteNumber);
        }

        throw new QuoteNotEditableException(QuoteNumber, Status);
    }
}
