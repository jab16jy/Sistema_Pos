namespace POS.Application.DTOs.Reporting;

/// <summary>
/// Recoverable result from GetOperationalReportUseCase.
/// IsSuccess = false means validation failed (e.g. invalid date range).
/// </summary>
public sealed record OperationalReportResult(bool IsSuccess, OperationalReportDto? Report, string? Error);
