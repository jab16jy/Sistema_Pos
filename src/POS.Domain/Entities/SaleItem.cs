using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class SaleItem
{
    private SaleItem()
    {
        ProductName = string.Empty;
    }

    public SaleItem(Guid productId, string productName, Quantity quantity, Money unitPrice, Money discount, Money unitCost = default)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = string.IsNullOrWhiteSpace(productName) ? throw new ArgumentException("Product name is required.", nameof(productName)) : productName.Trim();
        Quantity = quantity.Value;
        UnitPrice = unitPrice;
        Discount = discount;
        UnitCost = unitCost;
    }

    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }
    public decimal Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money Discount { get; private set; }

    /// <summary>
    /// Cost price snapshot captured at sale time from product.CostPrice.
    /// Used for gross margin computation. Defaults to zero for legacy rows
    /// (Infrastructure falls back to current Product.CostPrice when zero).
    /// </summary>
    public Money UnitCost { get; private set; }

    public Money Subtotal()
    {
        var gross = UnitPrice.Amount * Quantity;
        if (Discount.Amount > gross)
        {
            throw new InvalidOperationException("Item discount cannot exceed item gross total.");
        }

        return new Money(gross - Discount.Amount);
    }

    /// <summary>
    /// Gross profit for this line: Subtotal minus total cost.
    /// Returns zero if cost exceeds revenue (avoids negative Money).
    /// </summary>
    public Money Profit()
    {
        var subtotal = Subtotal().Amount;
        var totalCost = UnitCost.Amount * Quantity;
        if (subtotal <= totalCost)
        {
            return Money.Zero;
        }

        return new Money(subtotal - totalCost);
    }
}
