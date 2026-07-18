using POS.Domain.Enums;

namespace POS.Application.DTOs.Shifts;

public sealed record ShiftSummary(
    Guid Id,
    Guid CashierUserId,
    string CashierUsername,
    decimal OpeningCash,
    decimal? ClosingCash,
    ShiftStatus Status,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    string? Notes,
    bool HasClosingSummary = false,
    decimal SalesTotal = 0m,
    decimal CashPayments = 0m,
    decimal DigitalPayments = 0m,
    decimal ReceivableSettlementCash = 0m,
    decimal ReceivableSettlementDigital = 0m,
    decimal CashReturnOutflows = 0m,
    decimal DigitalReturnOutflows = 0m)
{
    public decimal ExpectedCash => OpeningCash + CashPayments + ReceivableSettlementCash - CashReturnOutflows;
}

public sealed record StartShiftRequest(
    decimal OpeningCash,
    string? Notes,
    DateTimeOffset? OpenedAt = null);

public sealed record CloseShiftRequest(Guid ShiftId, decimal ClosingCash, string? Notes);
