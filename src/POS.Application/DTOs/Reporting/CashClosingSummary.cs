namespace POS.Application.DTOs.Reporting;

/// <summary>
/// Result summary produced by CloseCashRegisterUseCase.
/// Exposes separated totals for auditing and reconciliation.
/// </summary>
public sealed record CashClosingSummary(
    decimal BilledTotal,
    decimal CashCollected,
    decimal DigitalCollected,
    decimal ReceivableSettlementTotal,
    decimal ExpectedDrawerCash,
    decimal CountedCash,
    decimal Difference,
    decimal ReceivableSettlementCash = 0m);
