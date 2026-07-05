using POS.Domain.Enums;
using POS.Domain.Exceptions;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class Product
{
    private Product()
    {
        Name = string.Empty;
        Description = string.Empty;
        SourceCode = string.Empty;
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
        SourceCode = barcode?.Value ?? $"MANUAL-{Id:N}";
        CategoryId = categoryId;
        CostPrice = costPrice;
        SalePrice = salePrice;
        CurrentStock = currentStock.Value;
        MinimumStock = minimumStock.Value;
        Barcode = barcode?.Value;
        ItemType = CatalogItemType.Product;
        TracksInventory = true;
        TaxRate = ValidateTaxRate(taxRate);
        IsActive = true;
    }

    public static Product CreateImportedCatalogItem(
        string sourceCode,
        string name,
        Guid categoryId,
        Money salePrice,
        StockQuantity currentStock,
        StockQuantity minimumStock,
        CatalogItemType itemType,
        bool tracksInventory,
        Guid? unitOfMeasureId,
        Guid? taxProfileId,
        string? description = null,
        Barcode? barcode = null,
        Money? costPrice = null,
        Guid? subCategoryId = null,
        decimal taxRate = 19m,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
            throw new ArgumentException("Source code is required.", nameof(sourceCode));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));

        return new Product
        {
            Id = Guid.NewGuid(),
            SourceCode = sourceCode.Trim(),
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            CategoryId = categoryId,
            SubCategoryId = subCategoryId,
            UnitOfMeasureId = unitOfMeasureId,
            TaxProfileId = taxProfileId,
            CostPrice = costPrice,
            SalePrice = salePrice,
            CurrentStock = tracksInventory ? currentStock.Value : 0m,
            MinimumStock = minimumStock.Value,
            Barcode = barcode?.Value,
            ItemType = itemType,
            TracksInventory = tracksInventory,
            TaxRate = ValidateTaxRate(taxRate),
            IsActive = isActive
        };
    }

    public Guid Id { get; private set; }
    public string SourceCode { get; private set; }
    public string? Barcode { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid? SubCategoryId { get; private set; }
    public Guid? UnitOfMeasureId { get; private set; }
    public Guid? TaxProfileId { get; private set; }
    public Money? CostPrice { get; private set; }
    public Money SalePrice { get; private set; }
    public decimal CurrentStock { get; private set; }
    public decimal MinimumStock { get; private set; }
    public CatalogItemType ItemType { get; private set; }
    public bool TracksInventory { get; private set; }
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
        if (!TracksInventory)
        {
            return;
        }

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
