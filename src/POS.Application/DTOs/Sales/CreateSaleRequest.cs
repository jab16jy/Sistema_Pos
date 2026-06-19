using POS.Domain.Enums;

namespace POS.Application.DTOs.Sales;

public sealed record CreateSaleRequest(
    Guid CashierUserId,
    Guid? CustomerId,
    IReadOnlyCollection<CreateSaleItemRequest> Items,
    IReadOnlyCollection<CreateSalePaymentRequest> Payments);

public sealed record CreateSaleItemRequest(
    Guid ProductId,
    decimal Quantity,
    decimal Discount);

public sealed record CreateSalePaymentRequest(
    PaymentMethod Method,
    decimal Amount,
    decimal? ReceivedAmount,
    string? BankOrPlatform);
