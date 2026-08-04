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
        IEnumerable<(string Name, int Units, Money Price)>? presentations = null)
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
        ReplacePresentations(presentations);
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

    private readonly List<ProductPresentation> _presentations = [];

    /// <summary>
    /// The packaged ways this shop chose to sell the product, in the order it arranged them.
    /// Empty means the product is only sold by the loose base unit.
    /// </summary>
    public IReadOnlyList<ProductPresentation> Presentations =>
        _presentations.OrderBy(presentation => presentation.DisplayOrder).ToList();

    /// <summary>True when the product can be sold this way. A blank name is the base unit, always available.</summary>
    public bool Supports(string? presentationName) =>
        string.IsNullOrWhiteSpace(presentationName) || Find(presentationName) is not null;

    /// <summary>Base inventory units contained in one of the chosen presentation.</summary>
    public int UnitsFor(string? presentationName) =>
        string.IsNullOrWhiteSpace(presentationName) ? 1 : Require(presentationName).Units;

    /// <summary>Sale price for the chosen presentation.</summary>
    public Money PriceFor(string? presentationName) =>
        string.IsNullOrWhiteSpace(presentationName) ? SalePrice : Require(presentationName).Price;

    /// <summary>Base inventory units consumed when selling <paramref name="quantity"/> of the chosen presentation.</summary>
    public decimal InventoryUnitsFor(string? presentationName, decimal quantity) =>
        quantity * UnitsFor(presentationName);

    /// <summary>Cost snapshot for one sold unit of the chosen presentation (per pack = per-unit cost * units).</summary>
    public Money UnitCostFor(string? presentationName) =>
        new((CostPrice?.Amount ?? 0m) * UnitsFor(presentationName));

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
        IEnumerable<(string Name, int Units, Money Price)>? presentations = null)
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
        ReplacePresentations(presentations);
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
    /// Replaces the whole set of presentations with what the shop configured, in the given order.
    /// The catalogue screen always sends the complete list, so replacing is the only mutation
    /// needed and a removed row simply stops being sent. Names must be unique per product —
    /// two presentations called "Caja" with different prices would make a sold line ambiguous.
    /// Past sale and quote lines keep their own name and unit snapshots, so editing here never
    /// rewrites history.
    /// </summary>
    public void ReplacePresentations(IEnumerable<(string Name, int Units, Money Price)>? presentations)
    {
        var incoming = new List<(string Name, int Units, Money Price)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, units, price) in presentations ?? [])
        {
            string normalized = ProductPresentation.Normalize(name);
            if (!seen.Add(normalized))
            {
                throw new ArgumentException($"Product '{Name}' already has a presentation named '{normalized}'.", nameof(presentations));
            }

            incoming.Add((normalized, units, price));
        }

        // A presentation that survives the edit keeps its identity: rebuilding it would make the
        // persistence layer delete and re-insert the same row on every save, which both floods the
        // audit trail and races the unique (product, name) index.
        _presentations.RemoveAll(presentation => !seen.Contains(presentation.Name));

        int order = 0;
        foreach (var (name, units, price) in incoming)
        {
            ProductPresentation? survivor = Find(name);
            if (survivor is null)
            {
                _presentations.Add(new ProductPresentation(name, units, price, order));
            }
            else
            {
                survivor.Reconfigure(name, units, price, order);
            }

            order++;
        }
    }

    private ProductPresentation? Find(string presentationName) =>
        _presentations.FirstOrDefault(presentation => presentation.Matches(presentationName));

    private ProductPresentation Require(string presentationName) =>
        Find(presentationName)
        ?? throw new InvalidOperationException($"Product '{Name}' is not configured to be sold as '{presentationName.Trim()}'.");

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
