using POS.Domain.Enums;

namespace POS.Application.DTOs.Sales;

public sealed record PaymentPreviewRequest(
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    decimal ReceivedAmount);
