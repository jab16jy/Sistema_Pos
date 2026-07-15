using POS.Domain.Enums;

namespace POS.Application.DTOs.Sales;

public sealed record PaymentPreviewRequest(
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    decimal ReceivedAmount,
    decimal DeferredAmount = 0m,
    string? BankOrPlatform = null);
