namespace POS.Application.DTOs.Sales;

public sealed record PaymentPreviewResult(
    bool CanConfirm,
    decimal ChangeAmount,
    string Message,
    decimal DeferredBalance = 0m);
