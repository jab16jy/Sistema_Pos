using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

/// <summary>
/// Represents a single line of a partial sale return.
/// Each item tracks which product was returned, how many units, and the refund amount.
/// </summary>
public sealed class SaleReturnItem
{
    private SaleReturnItem()
    {
        ProductName = string.Empty;
    }

    public SaleReturnItem(
        Guid saleReturnId,
        Guid saleItemId,
        Guid productId,
        string productName,
        int quantityReturned,
        Money unitPrice,
        Money refundAmount)
    {
        if (saleReturnId == Guid.Empty)
        {
            throw new ArgumentException("Sale return id is required.", nameof(saleReturnId));
        }

        if (saleItemId == Guid.Empty)
        {
            throw new ArgumentException("Sale item id is required.", nameof(saleItemId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product id is required.", nameof(productId));
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new ArgumentException("Product name is required.", nameof(productName));
        }

        if (quantityReturned <= 0)
        {
            throw new ArgumentException("Quantity returned must be greater than zero.", nameof(quantityReturned));
        }

        Id = Guid.NewGuid();
        SaleReturnId = saleReturnId;
        SaleItemId = saleItemId;
        ProductId = productId;
        ProductName = productName.Trim();
        QuantityReturned = quantityReturned;
        UnitPrice = unitPrice;
        RefundAmount = refundAmount;
    }

    public Guid Id { get; private set; }
    public Guid SaleReturnId { get; private set; }
    public Guid SaleItemId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; }

    /// <summary>Number of units being returned. Must be greater than zero.</summary>
    public int QuantityReturned { get; private set; }

    public Money UnitPrice { get; private set; }
    public Money RefundAmount { get; private set; }
}
