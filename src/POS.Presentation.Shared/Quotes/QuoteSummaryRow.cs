using System.Globalization;
using POS.Domain.Enums;

namespace POS.Presentation.Shared.Quotes;

public sealed record QuoteSummaryRow(
    Guid QuoteId,
    string QuoteNumber,
    string CustomerName,
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
}
