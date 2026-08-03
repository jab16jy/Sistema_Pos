using POS.Domain.Enums;

namespace POS.Application.DTOs.Sales;

public sealed record PaymentPreviewRequest(
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    decimal ReceivedAmount,
    decimal DeferredAmount = 0m,
    string? BankOrPlatform = null,
    IReadOnlyList<PaymentTender>? Tenders = null);

/// <summary>One leg of a split (multi-method) full payment — e.g. part cash, part transfer.</summary>
public sealed record PaymentTender(PaymentMethod Method, decimal Amount, string? BankOrPlatform = null);
