namespace POS.Domain.Enums;

/// <summary>
/// Single source for how a sold presentation reads on screens, receipts, invoices and quotes,
/// so the cart, the printed ticket and the PDF never disagree about what was sold.
/// A blank name is the loose base unit.
/// </summary>
public static class ProductPresentationLabel
{
    public const string UnitLabel = "Unidad";

    public static string For(string? presentationName, int unitsPerPresentation) =>
        string.IsNullOrWhiteSpace(presentationName)
            ? UnitLabel
            : unitsPerPresentation == 1
                ? presentationName.Trim()
                : $"{presentationName.Trim()} x{unitsPerPresentation}";
}
