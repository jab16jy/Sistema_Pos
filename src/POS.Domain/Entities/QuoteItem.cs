using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class QuoteItem
{
    private QuoteItem()
    {
        ProductName = string.Empty;
    }

    public QuoteItem(
        Guid productId,
        string productName,
        Money unitPrice,
        Quantity quantity,
        decimal discount,
        string? presentationName = null,
        int unitsPerPresentation = 1)
    {
        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ArgumentException("Product name is required.", nameof(productName));
        }

        if (discount < 0m || discount > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(discount), "Discount must be between 0 and 100.");
        }

        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = productName.Trim();
        UnitPrice = unitPrice;
        Quantity = quantity;
        Discount = discount;
        PresentationName = string.IsNullOrWhiteSpace(presentationName) ? null : presentationName.Trim();
        UnitsPerPresentation = ValidateUnitsPerPresentation(PresentationName, unitsPerPresentation);
    }

    /// <summary>
    /// A quote line. Every property is <c>private set</c> and that is a LOAD-BEARING constraint, not a
    /// style choice: <c>QuoteRepository.UpdateAsync</c> infers "a line that survived the edit needs no
    /// write" from the absence of mutators. Add one and that write silently stops happening — the edit
    /// reports success, the row keeps the old value, and the persisted total quietly disagrees with the
    /// one <c>Quote.Total</c> just computed.
    ///
    /// Correcting a quantity, price or discount is expressed by constructing a NEW item and calling
    /// <c>Quote.ReplaceItems</c>, which is why the repository can get away with that inference. Do not
    /// add a setter without changing how the repository decides a line needs a write.
    /// </summary>
    public Guid Id { get; private set; }
    public Guid QuoteId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }
    public Money UnitPrice { get; private set; }
    public Quantity Quantity { get; private set; }
    public decimal Discount { get; private set; }
    /// <summary>
    /// Name snapshot of the presentation quoted. Null means the loose base unit. A copy, not a
    /// foreign key, so a later catalogue rename never rewrites the quote that was agreed.
    /// </summary>
    public string? PresentationName { get; private set; }

    public int UnitsPerPresentation { get; private set; }

    public Money Subtotal() => new(UnitPrice.Amount * Quantity.Value * (1m - Discount / 100m));

    private static int ValidateUnitsPerPresentation(string? presentationName, int unitsPerPresentation)
    {
        if (presentationName is null)
        {
            return 1;
        }

        if (unitsPerPresentation < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(unitsPerPresentation), "A packaged presentation must contain at least 1 base unit.");
        }

        return unitsPerPresentation;
    }
}
