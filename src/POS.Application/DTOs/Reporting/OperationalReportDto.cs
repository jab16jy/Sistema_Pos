using System.Collections.Generic;

namespace POS.Application.DTOs.Reporting;

public sealed record OperationalReportChartPointDto(
    string Label,
    decimal Value,
    string ToolTip);

/// <summary>
/// Read model returned by GetOperationalReportUseCase.
/// Separates physical cash from digital collections and receivable settlements.
/// </summary>
public sealed record OperationalReportDto(
    decimal BilledSalesTotal,
    decimal SaleCashCollected,
    decimal SaleDigitalCollected,
    decimal ReceivableSettlementCash,
    decimal ReceivableSettlementDigital,
    decimal ReceivableSettlementTotal,
    decimal ExpectedDrawerCash,
    IReadOnlyList<OperationalReportChartPointDto> ChartPoints);
