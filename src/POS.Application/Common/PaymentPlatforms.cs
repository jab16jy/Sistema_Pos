namespace POS.Application.Common;

/// <summary>
/// Canonical list of accepted payment platforms for digital/transfer payments.
/// Validation must happen in the Application layer before the sale is persisted.
/// </summary>
public static class PaymentPlatforms
{
    public static readonly IReadOnlyList<string> All =
    [
        "Nequi",
        "Bancolombia",
        "Daviplata",
        "Efectivo"
    ];

    /// <summary>
    /// Returns true if the given platform name is in the canonical list (case-insensitive).
    /// Null or whitespace always returns false.
    /// </summary>
    public static bool IsValid(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform))
        {
            return false;
        }

        return All.Any(p => string.Equals(p, platform, StringComparison.OrdinalIgnoreCase));
    }
}
