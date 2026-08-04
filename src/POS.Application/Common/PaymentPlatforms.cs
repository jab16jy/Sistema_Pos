using POS.Domain.Enums;

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
        "Daviplata"
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

    public static void Validate(PaymentMethod method, string? platform)
    {
        bool supplied = !string.IsNullOrWhiteSpace(platform);

        if (method == PaymentMethod.Transfer)
        {
            if (!supplied)
            {
                throw new InvalidOperationException("Selecciona la plataforma de la transferencia.");
            }

            if (!IsValid(platform))
            {
                throw new InvalidOperationException(
                    $"La plataforma de pago '{platform}' no está aceptada. " +
                    $"Plataformas válidas: {string.Join(", ", All)}.");
            }

            return;
        }

        if (supplied)
        {
            throw new InvalidOperationException("La plataforma de pago solo aplica a transferencias.");
        }
    }
}
