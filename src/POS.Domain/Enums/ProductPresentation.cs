namespace POS.Domain.Enums;

/// <summary>
/// How a product is sold: as a single unit, or as one of the packaged presentations.
/// Each packaged presentation carries its own base-unit count and its own price, both
/// configured per product in the catalogue — they are never derived from one another.
/// </summary>
public enum ProductPresentation
{
    Unit = 1,
    Box = 2,
    HalfBox = 3,
    QuarterBox = 4
}
