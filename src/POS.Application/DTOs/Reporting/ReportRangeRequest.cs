namespace POS.Application.DTOs.Reporting;

/// <summary>
/// Defines the date range for an operational report query.
/// </summary>
public sealed record ReportRangeRequest(DateTimeOffset From, DateTimeOffset To);
