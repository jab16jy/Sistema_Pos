namespace POS.Application.DTOs.Receivables;

/// <summary>
/// Denormalized read model for displaying a customer receivable in the ReceivablesViewModel.
/// </summary>
public sealed record ReceivableSummary(
    Guid ReceivableId,
    Guid CustomerId,
    string CustomerName,
    Guid SaleId,
    string InvoiceNumber,
    decimal OriginalBalance,
    decimal OutstandingBalance,
    DateTimeOffset CreatedAt);
