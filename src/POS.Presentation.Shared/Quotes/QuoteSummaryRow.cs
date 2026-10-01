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
    string ExpiresAt,
    Guid? CustomerId = null,
    bool HasSuccessfulEmail = false,
    string? ReminderLabel = null)
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
            summary.ExpiresAt,
            summary.CustomerId,
            summary.HasSuccessfulEmail,
            summary.ReminderLabel);

    /// <summary>
    /// True when this quote references a customer registered in the system, as opposed to a walk-in/casual
    /// name typed freehand. Customers are never hard-deleted in this system (Customer only exposes
    /// Deactivate(), and ICustomerRepository has no DeleteAsync), so a non-null CustomerId always still
    /// resolves to a real (if possibly inactive) customer row — no dangling-reference staleness to guard against.
    /// </summary>
    public bool IsRegisteredCustomer => CustomerId.HasValue;

    public string CustomerBadgeLabel => IsRegisteredCustomer ? string.Empty : "No registrado";

    private static string ResolveStatusLabel(QuoteStatus status) => status switch
    {
        QuoteStatus.Draft => "Borrador",
        QuoteStatus.Active => "Activa",
        QuoteStatus.Expired => "Expirada",
        QuoteStatus.Converted => "Convertida",
        QuoteStatus.Cancelled => "Anulada",
        _ => status.ToString()
    };

    /// <summary>
    /// Chip color variant for the status badge ("success"/"warning"/"error"/"neutral"). Domain only models
    /// Draft/Active/Expired/Converted/Cancelled.
    /// </summary>
    public string StatusChipKind => Status switch
    {
        QuoteStatus.Active => "success",
        QuoteStatus.Draft => "warning",
        QuoteStatus.Expired => "error",
        QuoteStatus.Cancelled => "error",
        QuoteStatus.Converted => "neutral",
        _ => "neutral"
    };

    /// <summary>
    /// True when the "Venta" row action should be shown. Mirrors <c>ConvertQuoteToSaleUseCase</c>'s own guard
    /// (blocks Converted/Expired). New quotes are created as Draft, and the documented business rule allows
    /// Draft or Active quotes to be converted.
    /// </summary>
    public bool HasReminder => HasSuccessfulEmail && !string.IsNullOrWhiteSpace(ReminderLabel);

    public bool IsExpiredByCalendar => string.Equals(ReminderLabel, "Vencida", StringComparison.Ordinal);

    public string ExpirationGuidance => IsExpiredByCalendar
        ? "Renueva o crea otra cotización para vender."
        : string.Empty;

    public bool CanConvertToSale => !IsExpiredByCalendar && Status is QuoteStatus.Draft or QuoteStatus.Active;

    /// <summary>
    /// True when the quote is still a proposal that may be corrected. Mirrors <c>Quote.CanEdit</c>
    /// exactly — the rule is the aggregate's, this only restates it so the row can drive a binding.
    /// Whether the operator is *allowed* to edit is a permission question and lives in the view-model.
    /// </summary>
    public bool CanEdit => Status is QuoteStatus.Draft or QuoteStatus.Active;

    public bool CanCancel => Status is QuoteStatus.Draft or QuoteStatus.Active or QuoteStatus.Expired;
}
