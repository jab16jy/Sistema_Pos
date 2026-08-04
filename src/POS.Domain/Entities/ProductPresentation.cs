using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

/// <summary>
/// A packaged way of selling a product that the shop defines itself: a box, a pack of three,
/// a dozen — whatever it names. Units and price are configured together and never derived from
/// one another, so a pack of three may be cheaper or dearer than three loose units.
/// Selling by the loose base unit needs no presentation; it is always available.
/// </summary>
public sealed class ProductPresentation
{
    /// <summary>The name every new product starts with, so the common case needs no typing.</summary>
    public const string DefaultName = "Caja";

    private ProductPresentation()
    {
        Name = string.Empty;
    }

    public ProductPresentation(string name, int units, Money price, int displayOrder = 0)
    {
        Id = Guid.NewGuid();
        Name = Normalize(name);
        Units = units >= 2
            ? units
            : throw new ArgumentOutOfRangeException(nameof(units), "A presentation must contain at least 2 base units.");
        Price = price;
        DisplayOrder = displayOrder;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Name { get; private set; }

    /// <summary>Base inventory units contained in one of these.</summary>
    public int Units { get; private set; }

    /// <summary>Own price for one of these, never derived from the base unit price.</summary>
    public Money Price { get; private set; }

    /// <summary>Preserves the order the shop arranged its presentations in.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Presentations are matched by name, case-insensitively, so "caja" finds "Caja".</summary>
    public bool Matches(string name) => string.Equals(Name, name?.Trim(), StringComparison.OrdinalIgnoreCase);

    internal static string Normalize(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("A presentation needs a name.", nameof(name))
            : name.Trim();
}
