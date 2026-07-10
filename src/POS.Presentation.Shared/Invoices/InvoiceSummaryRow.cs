using System.Globalization;
using POS.Application.DTOs.Invoices;
using POS.Domain.Enums;

namespace POS.Presentation.Shared.Invoices;

/// <summary>
/// Presentation-only projection of <see cref="SaleHistoryResult"/> for the Facturas grid: formatted totals/dates
/// and a wireframe-aligned status chip/label/filter-key, mirroring the <c>QuoteSummaryRow</c> pattern used for
/// Cotizaciones history.
/// </summary>
public sealed record InvoiceSummaryRow(
    Guid SaleId,
    string InvoiceNumber,
    string CustomerName,
    decimal Total,
    string TotalFormatted,
    SaleStatus Status,
    string StatusLabel,
    DateTimeOffset CreatedAt,
    string DateFormatted,
    string CashierName = "—")
{
    private static readonly CultureInfo ColombianCulture = CultureInfo.GetCultureInfo("es-CO");

    public static InvoiceSummaryRow FromSale(SaleHistoryResult sale) => new(
        sale.SaleId,
        sale.InvoiceNumber,
        string.IsNullOrWhiteSpace(sale.CustomerName) ? "— Consumidor final —" : sale.CustomerName,
        sale.Total,
        $"${sale.Total.ToString("N0", ColombianCulture)}",
        sale.Status,
        ResolveStatusLabel(sale.Status),
        sale.CreatedAt,
        sale.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        string.IsNullOrWhiteSpace(sale.CashierName) ? "—" : sale.CashierName);

    public static InvoiceSummaryRow FromDetail(InvoiceDetailResult detail) => new(
        detail.SaleId,
        detail.InvoiceNumber,
        string.IsNullOrWhiteSpace(detail.CustomerName) ? "— Consumidor final —" : detail.CustomerName,
        detail.Total,
        $"${detail.Total.ToString("N0", ColombianCulture)}",
        detail.Status,
        ResolveStatusLabel(detail.Status),
        detail.CreatedAt,
        detail.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        string.IsNullOrWhiteSpace(detail.CashierName) ? "—" : detail.CashierName);

    private static string ResolveStatusLabel(SaleStatus status) => status switch
    {
        SaleStatus.Completed => "Activa",
        SaleStatus.Cancelled => "Anulada",
        SaleStatus.Returned => "Devuelta",
        SaleStatus.Draft => "Borrador",
        SaleStatus.OnHold => "En espera",
        _ => status.ToString()
    };

    /// <summary>
    /// Chip color variant for the status badge ("success"/"warning"/"error"/"neutral"). Maps the domain's
    /// 5-state <see cref="SaleStatus"/> onto the wireframe's 3-state filter (activa/anulada/devuelta);
    /// Draft/OnHold sales never reach invoice history in practice (only completed sales are invoiced) but
    /// are mapped defensively rather than left unstyled.
    /// </summary>
    public string StatusChipKind => Status switch
    {
        SaleStatus.Completed => "success",
        SaleStatus.Cancelled => "error",
        SaleStatus.Returned => "warning",
        _ => "neutral"
    };

    /// <summary>Status filter key matching the wireframe's status selector values ("activa"/"anulada"/"devuelta").</summary>
    public string StatusFilterKey => Status switch
    {
        SaleStatus.Completed => "activa",
        SaleStatus.Cancelled => "anulada",
        SaleStatus.Returned => "devuelta",
        _ => "otro"
    };

    /// <summary>True when the row's own "Dev." quick-select action should render (only for active/completed invoices).</summary>
    public bool CanReturn => Status == SaleStatus.Completed;
}
