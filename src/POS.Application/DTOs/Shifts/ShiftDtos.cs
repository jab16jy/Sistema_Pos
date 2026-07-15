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
    decimal ReceivableSettlementDigital = 0m)
{
    public decimal ExpectedCash => OpeningCash + CashPayments + ReceivableSettlementCash;
}

public sealed record StartShiftRequest(
    Guid CashierUserId,
    string CashierUsername,
    decimal OpeningCash,
    string? Notes,
    DateTimeOffset? OpenedAt = null);

public sealed record CloseShiftRequest(Guid ShiftId, decimal ClosingCash, string? Notes);
