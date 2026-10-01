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
    string? ReminderLabel = null,
    bool IsExpiredByCalendar = false)
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
            summary.ReminderLabel,
            summary.IsExpiredByCalendar);

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
    /// True when the expiry chip should be shown. Reads the label alone, with no send-history gate: a
    /// deadline elapsing is a calendar fact about the quote, not a fact about whether an email went out.
    /// Gating this on HasSuccessfulEmail — as this did — hid "Vencida" for every quote that was never
    /// sent, and took the renewal guidance with it.
    /// </summary>
    public bool HasReminder => !string.IsNullOrWhiteSpace(ReminderLabel);

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
    /// Renewal guidance for a lapsed proposal. Driven by the same field as the chip, so the operator is
    /// told what to do about an expired quote whether or not it was ever emailed.
    /// </summary>
    public string ExpirationGuidance => IsExpiredByCalendar
        ? "Renueva o crea otra cotización para vender."
        : string.Empty;

    /// <summary>
    /// True when the "Venta" row action should be shown. Mirrors <c>ConvertQuoteToSaleUseCase</c>'s own guard
    /// (blocks Converted/Expired). New quotes are created as Draft, and the documented business rule allows
    /// Draft or Active quotes to be converted.
    /// </summary>
    public bool CanConvertToSale => !IsExpiredByCalendar && Status is QuoteStatus.Draft or QuoteStatus.Active;

    /// <summary>
    /// True when the quote is still a proposal that may be corrected, mirroring <c>Quote.CanEdit</c> — the
    /// rule is the aggregate's, this only restates it so the row can drive a binding. That rule requires
    /// an editable status AND a deadline that has not lapsed, so a lapsed Draft (which no caller has
    /// transitioned to Expired) loses its affordance here too. Whether the operator is *allowed* to edit
    /// is a permission question and lives in the view-model.
    /// </summary>
    public bool CanEdit => !IsExpiredByCalendar && Status is QuoteStatus.Draft or QuoteStatus.Active;

    public bool CanCancel => Status is QuoteStatus.Draft or QuoteStatus.Active or QuoteStatus.Expired;
}
