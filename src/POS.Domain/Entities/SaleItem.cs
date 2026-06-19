using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class SaleItem
{
    private SaleItem()
    {
        ProductName = string.Empty;
    }

    public SaleItem(Guid productId, string productName, Quantity quantity, Money unitPrice, Money discount)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = string.IsNullOrWhiteSpace(productName) ? throw new ArgumentException("Product name is required.", nameof(productName)) : productName.Trim();
        Quantity = quantity.Value;
        UnitPrice = unitPrice;
        Discount = discount;
    }

    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }
    public decimal Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money Discount { get; private set; }

    public Money Subtotal()
    {
        var gross = UnitPrice.Amount * Quantity;
        if (Discount.Amount > gross)
        {
            throw new InvalidOperationException("Item discount cannot exceed item gross total.");
        }

        return new Money(gross - Discount.Amount);
    }
}
