namespace POS.Application.DTOs.Reporting;

/// <summary>
/// Recoverable result from CloseCashRegisterUseCase.
/// IsSuccess = false means validation failed (e.g. missing counted cash).
/// </summary>
public sealed record CashClosingResult(bool IsSuccess, CashClosingSummary? Summary, string? Error);
