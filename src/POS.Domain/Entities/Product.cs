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
        Money? costPrice,
        Money salePrice,
        StockQuantity currentStock,
        StockQuantity minimumStock,
        string? description = null,
        Barcode? barcode = null,
        decimal taxRate = 19m,
        int? unitsPerBox = null,
        Money? boxPrice = null)
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
        SetBoxPresentation(unitsPerBox, boxPrice);
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

    /// <summary>Units contained in one box, when the product can also be sold by the box. Null = unit-only.</summary>
    public int? UnitsPerBox { get; private set; }

    /// <summary>Independent price for one box (not necessarily UnitsPerBox * SalePrice). Null = unit-only.</summary>
    public Money? BoxPrice { get; private set; }

    /// <summary>True when the product can be sold as a box, not just by unit.</summary>
    public bool SupportsBoxSale => UnitsPerBox is int units && units > 0 && BoxPrice is not null;

    /// <summary>Sale price for the chosen presentation.</summary>
    public Money PriceFor(ProductPresentation presentation) =>
        presentation == ProductPresentation.Box
            ? RequireBox().BoxPrice!.Value
            : SalePrice;

    /// <summary>Base inventory units consumed when selling <paramref name="quantity"/> of the chosen presentation.</summary>
    public decimal InventoryUnitsFor(ProductPresentation presentation, decimal quantity) =>
        presentation == ProductPresentation.Box
            ? quantity * RequireBox().UnitsPerBox!.Value
            : quantity;

    /// <summary>Cost snapshot for one sold unit of the chosen presentation (per box = per-unit cost * units).</summary>
    public Money UnitCostFor(ProductPresentation presentation)
    {
        decimal unitCost = CostPrice?.Amount ?? 0m;
        return presentation == ProductPresentation.Box
            ? new Money(unitCost * RequireBox().UnitsPerBox!.Value)
            : new Money(unitCost);
    }

    public bool IsLowStock() => CurrentStock <= MinimumStock;

    public void Deactivate() => IsActive = false;

    public void SetSubCategory(Guid? subCategoryId) => SubCategoryId = subCategoryId;

    public void SetUnitOfMeasure(Guid? unitOfMeasureId) => UnitOfMeasureId = unitOfMeasureId;

    public void UpdateDetails(
        string name,
        Guid categoryId,
        Money? costPrice,
        Money salePrice,
        StockQuantity minimumStock,
        string? description = null,
        Barcode? barcode = null,
        decimal taxRate = 19m,
        int? unitsPerBox = null,
        Money? boxPrice = null)
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
        SetBoxPresentation(unitsPerBox, boxPrice);
    }

    public void AssignBarcode(Barcode barcode)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Cannot assign a barcode to an inactive product.");
        }

        if (!string.IsNullOrWhiteSpace(Barcode))
        {
            throw new InvalidOperationException("Product already has a barcode.");
        }

        Barcode = barcode.Value;
    }

    /// <summary>
    /// Sets (or clears) the box presentation. Both values go together: a box needs a
    /// unit count (at least 2) and its own price. Passing neither clears box selling.
    /// </summary>
    private void SetBoxPresentation(int? unitsPerBox, Money? boxPrice)
    {
        if (unitsPerBox is null && boxPrice is null)
        {
            UnitsPerBox = null;
            BoxPrice = null;
            return;
        }

        if (unitsPerBox is null || boxPrice is null)
        {
            throw new ArgumentException("A box presentation needs both the units per box and the box price.");
        }

        if (unitsPerBox.Value < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(unitsPerBox), "A box must contain at least 2 units.");
        }

        UnitsPerBox = unitsPerBox.Value;
        BoxPrice = boxPrice.Value;
    }

    private Product RequireBox() =>
        SupportsBoxSale ? this : throw new InvalidOperationException($"Product '{Name}' is not configured for box sales.");

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
