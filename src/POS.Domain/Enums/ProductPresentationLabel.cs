namespace POS.Domain.Enums;

/// <summary>
/// Single source for how a sold presentation reads on screens, receipts, invoices and quotes,
/// so the cart, the printed ticket and the PDF never disagree about what was sold.
/// </summary>
public static class ProductPresentationLabel
{
    public static string For(ProductPresentation presentation, int unitsPerPresentation) => presentation switch
    {
        ProductPresentation.Box => $"Caja x{unitsPerPresentation}",
        ProductPresentation.HalfBox => $"Media caja x{unitsPerPresentation}",
        ProductPresentation.QuarterBox => $"Cuarto de caja x{unitsPerPresentation}",
        _ => "Unidad"
    };
}
