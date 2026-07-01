using System.Globalization;
using POS.Domain.Enums;

namespace POS.Presentation.Shared.Quotes;

public sealed record QuoteSummaryRow(
    Guid QuoteId,
    string QuoteNumber,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    decimal Total,
    string TotalFormatted,
    QuoteStatus Status,
    string StatusLabel,
    string ExpiresAt)
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    public static QuoteSummaryRow FromSummary(Application.DTOs.Quotes.QuoteSummary summary) =>
        new(
            summary.QuoteId,
            summary.QuoteNumber,
            summary.CustomerName,
            string.IsNullOrWhiteSpace(summary.CustomerEmail) ? null : summary.CustomerEmail,
            string.IsNullOrWhiteSpace(summary.CustomerPhone) ? null : summary.CustomerPhone,
            summary.Total,
            $"${summary.Total.ToString("N0", ColombianCulture)}",
            summary.Status,
            ResolveStatusLabel(summary.Status),
            summary.ExpiresAt);

    private static string ResolveStatusLabel(QuoteStatus status) => status switch
    {
        QuoteStatus.Draft => "Borrador",
        QuoteStatus.Active => "Activa",
        QuoteStatus.Expired => "Expirada",
        QuoteStatus.Converted => "Convertida",
        _ => status.ToString()
    };

    /// <summary>
    /// Chip color variant for the status badge ("success"/"warning"/"error"/"neutral"). Domain only models
    /// Draft/Active/Expired/Converted (no separate "enviada"/"aceptada"/"rechazada" states like the wireframe
    /// mock); Active is the closest equivalent to the wireframe's "aceptada" (ready-to-convert) state.
    /// </summary>
    public string StatusChipKind => Status switch
    {
        QuoteStatus.Active => "success",
        QuoteStatus.Draft => "warning",
        QuoteStatus.Expired => "error",
        QuoteStatus.Converted => "neutral",
        _ => "neutral"
    };

    /// <summary>
    /// True when the "Venta" row action should be shown. Mirrors <c>ConvertQuoteToSaleUseCase</c>'s own guard
    /// (blocks Converted/Expired), matching the wireframe's "only 'aceptada' quotes convert" intent as closely
    /// as the real domain status set allows.
    /// </summary>
    public bool CanConvertToSale => Status == QuoteStatus.Active;
}
