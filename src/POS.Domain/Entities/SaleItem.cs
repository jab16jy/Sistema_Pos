using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class SaleItem
{
    private SaleItem()
    {
        ProductName = string.Empty;
    }

    public SaleItem(
        Guid productId,
        string productName,
        Quantity quantity,
        Money unitPrice,
        Money discount,
        Money unitCost = default,
        decimal taxRate = 19m,
        string? presentationName = null,
        int unitsPerPresentation = 1)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = string.IsNullOrWhiteSpace(productName) ? throw new ArgumentException("Product name is required.", nameof(productName)) : productName.Trim();
        Quantity = quantity.Value;
        UnitPrice = unitPrice;
        Discount = discount;
        UnitCost = unitCost;
        TaxRate = ValidateTaxRate(taxRate);
        PresentationName = string.IsNullOrWhiteSpace(presentationName) ? null : presentationName.Trim();
        UnitsPerPresentation = ValidateUnitsPerPresentation(PresentationName, unitsPerPresentation);
    }

    public Guid Id { get; private set; }
    public Guid SaleId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }
    public decimal Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money Discount { get; private set; }
    public decimal TaxRate { get; private set; }
    /// <summary>
    /// Name snapshot of the presentation sold, copied at sale time. Null means the loose base unit.
    /// Deliberately a copy and not a foreign key: renaming or deleting a presentation in the
    /// catalogue must never rewrite what a past invoice says was sold.
    /// </summary>
    public string? PresentationName { get; private set; }

    public int UnitsPerPresentation { get; private set; }

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

    public Money TaxAmount() => new(IncludedTax(Subtotal().Amount, TaxRate));

    /// <summary>
    /// Tax already contained inside a tax-inclusive subtotal, for a rate expressed as a percentage.
    /// Shared with the cashier preview so the figure shown before confirming a sale is the same one
    /// the invoice prints afterwards.
    /// </summary>
    public static decimal IncludedTax(decimal subtotal, decimal taxRatePercent)
        => taxRatePercent <= 0m
            ? 0m
            : Math.Round(subtotal * taxRatePercent / (100m + taxRatePercent), 2, MidpointRounding.AwayFromZero);

    public Money TaxableBaseAmount()
    {
        var subtotal = Subtotal().Amount;
        var tax = TaxAmount().Amount;
        return tax >= subtotal ? Money.Zero : new Money(subtotal - tax);
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

    private static decimal ValidateTaxRate(decimal taxRate)
    {
        if (taxRate < 0m || taxRate > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(taxRate), "Tax rate must be between 0 and 100.");
        }

        return taxRate;
    }

    private static int ValidateUnitsPerPresentation(string? presentationName, int unitsPerPresentation)
    {
        if (presentationName is null)
        {
            return 1;
        }

        if (unitsPerPresentation < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(unitsPerPresentation), "A packaged presentation must contain at least 2 base units.");
        }

        return unitsPerPresentation;
    }
}
