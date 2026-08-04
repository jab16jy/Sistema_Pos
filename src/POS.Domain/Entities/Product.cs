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
        Money? boxPrice = null,
        int? halfBoxUnits = null,
        Money? halfBoxPrice = null,
        int? quarterBoxUnits = null,
        Money? quarterBoxPrice = null)
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
        SetPackagedPresentations(unitsPerBox, boxPrice, halfBoxUnits, halfBoxPrice, quarterBoxUnits, quarterBoxPrice);
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
    public int Version { get; private set; }

    /// <summary>Units contained in one box, when the product can also be sold by the box. Null = not sold as a box.</summary>
    public int? UnitsPerBox { get; private set; }

    /// <summary>Independent price for one box (not necessarily UnitsPerBox * SalePrice). Null = not sold as a box.</summary>
    public Money? BoxPrice { get; private set; }

    /// <summary>Units contained in one half box. Configured explicitly, never derived from UnitsPerBox.</summary>
    public int? HalfBoxUnits { get; private set; }

    /// <summary>Independent price for one half box. Null = not sold as a half box.</summary>
    public Money? HalfBoxPrice { get; private set; }

    /// <summary>Units contained in one quarter box. Configured explicitly, never derived from UnitsPerBox.</summary>
    public int? QuarterBoxUnits { get; private set; }

    /// <summary>Independent price for one quarter box. Null = not sold as a quarter box.</summary>
    public Money? QuarterBoxPrice { get; private set; }

    /// <summary>True when the product can be sold as a box, not just by unit.</summary>
    public bool SupportsBoxSale => Supports(ProductPresentation.Box);

    /// <summary>True when the product can be sold in the given presentation. Unit is always available.</summary>
    public bool Supports(ProductPresentation presentation) =>
        presentation == ProductPresentation.Unit || PackagedPresentation(presentation) is not null;

    /// <summary>Base inventory units contained in one of the chosen presentation.</summary>
    public int UnitsFor(ProductPresentation presentation) =>
        presentation == ProductPresentation.Unit ? 1 : Require(presentation).Units;

    /// <summary>Sale price for the chosen presentation.</summary>
    public Money PriceFor(ProductPresentation presentation) =>
        presentation == ProductPresentation.Unit ? SalePrice : Require(presentation).Price;

    /// <summary>Base inventory units consumed when selling <paramref name="quantity"/> of the chosen presentation.</summary>
    public decimal InventoryUnitsFor(ProductPresentation presentation, decimal quantity) =>
        quantity * UnitsFor(presentation);

    /// <summary>Cost snapshot for one sold unit of the chosen presentation (per box = per-unit cost * units).</summary>
    public Money UnitCostFor(ProductPresentation presentation) =>
        new((CostPrice?.Amount ?? 0m) * UnitsFor(presentation));

    public bool IsLowStock() => MinimumStock > 0m && CurrentStock > 0m && CurrentStock <= MinimumStock;

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
        Money? boxPrice = null,
        int? halfBoxUnits = null,
        Money? halfBoxPrice = null,
        int? quarterBoxUnits = null,
        Money? quarterBoxPrice = null)
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
        SetPackagedPresentations(unitsPerBox, boxPrice, halfBoxUnits, halfBoxPrice, quarterBoxUnits, quarterBoxPrice);
    }

    public void AssignCategory(Guid categoryId)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Cannot update an inactive product.");
        }

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category is required.", nameof(categoryId));
        }

        CategoryId = categoryId;
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
    /// Sets (or clears) each packaged presentation. Units and price go together per
    /// presentation: a package needs a unit count (at least 2) and its own price, and
    /// passing neither clears that presentation. Half and quarter box are independent
    /// of the box — a shop may price a half box without offering the full one.
    /// </summary>
    private void SetPackagedPresentations(
        int? unitsPerBox,
        Money? boxPrice,
        int? halfBoxUnits,
        Money? halfBoxPrice,
        int? quarterBoxUnits,
        Money? quarterBoxPrice)
    {
        (UnitsPerBox, BoxPrice) = ValidatePackage("box", unitsPerBox, boxPrice, nameof(unitsPerBox));
        (HalfBoxUnits, HalfBoxPrice) = ValidatePackage("half box", halfBoxUnits, halfBoxPrice, nameof(halfBoxUnits));
        (QuarterBoxUnits, QuarterBoxPrice) = ValidatePackage("quarter box", quarterBoxUnits, quarterBoxPrice, nameof(quarterBoxUnits));
    }

    private static (int?, Money?) ValidatePackage(string label, int? units, Money? price, string unitsParameterName)
    {
        if (units is null && price is null)
        {
            return (null, null);
        }

        if (units is null || price is null)
        {
            throw new ArgumentException($"A {label} presentation needs both the units it contains and its price.");
        }

        if (units.Value < 2)
        {
            throw new ArgumentOutOfRangeException(unitsParameterName, $"A {label} must contain at least 2 units.");
        }

        return (units.Value, price.Value);
    }

    private (int Units, Money Price)? PackagedPresentation(ProductPresentation presentation) => presentation switch
    {
        ProductPresentation.Box => Package(UnitsPerBox, BoxPrice),
        ProductPresentation.HalfBox => Package(HalfBoxUnits, HalfBoxPrice),
        ProductPresentation.QuarterBox => Package(QuarterBoxUnits, QuarterBoxPrice),
        _ => null
    };

    private static (int Units, Money Price)? Package(int? units, Money? price) =>
        units is int configuredUnits && configuredUnits > 0 && price is Money configuredPrice
            ? (configuredUnits, configuredPrice)
            : null;

    private (int Units, Money Price) Require(ProductPresentation presentation) =>
        PackagedPresentation(presentation)
        ?? throw new InvalidOperationException($"Product '{Name}' is not configured for {presentation} sales.");

    /// <summary>
    /// The single entry point that changes <see cref="CurrentStock"/> and produces the matching
    /// <see cref="InventoryMovement"/>, so the inventory invariant holds: one stock change ⇒ exactly
    /// one movement, created together. A positive <paramref name="delta"/> adds stock
    /// (Entry/InitialBalance/Return or a positive Adjustment); a negative delta removes it (a negative
    /// Adjustment) and is rejected when it would drive stock below zero. Sales issue their Exit
    /// movement separately because the aggregate already decremented stock when the line was added.
    /// </summary>
    public InventoryMovement RegisterStockMovement(
        InventoryMovementType type,
        decimal delta,
        string reason,
        Guid userId,
        Guid? referenceId = null)
    {
        if (delta == 0m)
        {
            throw new ArgumentException("A stock movement must change the quantity.", nameof(delta));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A stock movement requires an operator for audit traceability.", nameof(userId));
        }

        if (delta > 0m)
        {
            IncreaseStock(new Quantity(delta));
        }
        else
        {
            DecreaseStock(new Quantity(Math.Abs(delta)));
        }

        return new InventoryMovement(Id, type, new Quantity(Math.Abs(delta)), reason, userId, referenceId);
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

    public void IncreaseStock(Quantity quantity)
    {
        if (!TracksInventory)
        {
            return;
        }

        CurrentStock += quantity.Value;
    }

    public void ApplyPurchaseReceiptCost(decimal priorStock, Quantity receivedUnits, Money receivedBaseUnitCost)
    {
        if (priorStock < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(priorStock));
        }

        if (priorStock == 0m)
        {
            CostPrice = receivedBaseUnitCost;
            return;
        }

        // A cost cannot be inferred for stock which predates reliable cost records.
        if (CostPrice is null)
        {
            return;
        }

        decimal weightedAverage = ((priorStock * CostPrice.Value.Amount) + (receivedUnits.Value * receivedBaseUnitCost.Amount))
            / (priorStock + receivedUnits.Value);
        CostPrice = new Money(weightedAverage);
    }

    private static decimal ValidateTaxRate(decimal taxRate)
    {
        if (taxRate < 0m || taxRate > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(taxRate), "Tax rate must be between 0 and 100.");
        }

        return taxRate;
    }
}
