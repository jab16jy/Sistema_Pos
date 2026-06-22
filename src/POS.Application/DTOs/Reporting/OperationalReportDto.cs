namespace POS.Application.DTOs.Reporting;

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
    decimal ExpectedDrawerCash);
