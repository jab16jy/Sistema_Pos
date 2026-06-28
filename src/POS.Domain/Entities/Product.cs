using POS.Domain.Exceptions;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class Product
{
    private Product()
    {
        Name = string.Empty;
        Description = string.Empty;
    }

    public Product(
        string name,
        Guid categoryId,
        Money costPrice,
        Money salePrice,
        StockQuantity currentStock,
        StockQuantity minimumStock,
        string? description = null,
        Barcode? barcode = null,
        decimal taxRate = 19m)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name is required.", nameof(name));
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        CategoryId = categoryId;
        CostPrice = costPrice;
        SalePrice = salePrice;
        CurrentStock = currentStock.Value;
        MinimumStock = minimumStock.Value;
        Barcode = barcode?.Value;
        TaxRate = ValidateTaxRate(taxRate);
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string? Barcode { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid? SubCategoryId { get; private set; }
    public Guid? UnitOfMeasureId { get; private set; }
    public Money CostPrice { get; private set; }
    public Money SalePrice { get; private set; }
    public decimal CurrentStock { get; private set; }
    public decimal MinimumStock { get; private set; }
    public decimal TaxRate { get; private set; }
    public bool IsActive { get; private set; }

    public bool IsLowStock() => CurrentStock <= MinimumStock;

    public void Deactivate() => IsActive = false;

    public void SetSubCategory(Guid? subCategoryId) => SubCategoryId = subCategoryId;

    public void SetUnitOfMeasure(Guid? unitOfMeasureId) => UnitOfMeasureId = unitOfMeasureId;

    public void UpdateDetails(
        string name,
        Guid categoryId,
        Money costPrice,
        Money salePrice,
        StockQuantity minimumStock,
        string? description = null,
        Barcode? barcode = null,
        decimal taxRate = 19m)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Cannot update an inactive product.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Product name is required.", nameof(name));
        }

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        CategoryId = categoryId;
        CostPrice = costPrice;
        SalePrice = salePrice;
        MinimumStock = minimumStock.Value;
        Barcode = barcode?.Value;
        TaxRate = ValidateTaxRate(taxRate);
    }

    public void AdjustStock(decimal delta)
    {
        if (delta == 0m)
        {
            throw new ArgumentException("Stock adjustment cannot be zero.", nameof(delta));
        }

        if (delta > 0m)
        {
            IncreaseStock(new Quantity(delta));
            return;
        }

        DecreaseStock(new Quantity(Math.Abs(delta)));
    }

    public void DecreaseStock(Quantity quantity)
    {
        if (CurrentStock < quantity.Value)
        {
            throw new InsufficientStockException(Name, CurrentStock, quantity.Value);
        }

        CurrentStock -= quantity.Value;
    }

    public void IncreaseStock(Quantity quantity) => CurrentStock += quantity.Value;

    private static decimal ValidateTaxRate(decimal taxRate)
    {
        if (taxRate < 0m || taxRate > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(taxRate), "Tax rate must be between 0 and 100.");
        }

        return taxRate;
    }
}
