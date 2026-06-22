namespace POS.Application.DTOs.Sales;

public sealed record CreateSaleResult(
    Guid SaleId,
    string InvoiceNumber,
    decimal Total,
    decimal CashInDrawer,
    decimal DeferredBalance = 0m);
