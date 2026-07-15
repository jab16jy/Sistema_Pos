using POS.Domain.Enums;
using POS.Domain.Entities;

namespace POS.Application.DTOs.Sales;

public sealed record HoldSaleRequest(
    Guid CashierUserId,
    Guid? CustomerId,
    IReadOnlyCollection<CreateSaleItemRequest> Items);

public sealed record HeldSaleItem(
    Guid ProductId,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    ProductPresentation Presentation,
    int UnitsPerPresentation);

public sealed record HeldSaleSummary(
    Guid SaleId,
    Guid CashierUserId,
    Guid? CustomerId,
    string InvoiceNumber,
    DateTimeOffset CreatedAt,
    decimal Total,
    IReadOnlyCollection<HeldSaleItem> Items);

public sealed record ResumeHeldSaleRequest(Guid CashierUserId, Guid SaleId);

public sealed record CompleteHeldSaleRequest(
    Guid CashierUserId,
    Guid SaleId,
    IReadOnlyCollection<CreateSalePaymentRequest> Payments,
    SaleSettlementPlan? SettlementPlan = null);
