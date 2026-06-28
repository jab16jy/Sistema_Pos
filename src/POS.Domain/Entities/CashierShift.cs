using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class CashierShift
{
    private CashierShift()
    {
        CashierUsername = string.Empty;
    }

    public CashierShift(Guid cashierUserId, string cashierUsername, Money openingCash, string? notes = null)
    {
        Id = Guid.NewGuid();
        CashierUserId = cashierUserId;
        CashierUsername = string.IsNullOrWhiteSpace(cashierUsername) ? "Cajero" : cashierUsername.Trim();
        OpeningCash = openingCash;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = ShiftStatus.Open;
        OpenedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid CashierUserId { get; private set; }
    public string CashierUsername { get; private set; }
    public Money OpeningCash { get; private set; }
    public Money ClosingCash { get; private set; }
    public string? Notes { get; private set; }
    public ShiftStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public void Close(Money closingCash, string? notes = null)
    {
        if (Status == ShiftStatus.Closed)
        {
            throw new InvalidOperationException("El turno ya está cerrado.");
        }

        ClosingCash = closingCash;
        Notes = string.IsNullOrWhiteSpace(notes) ? Notes : notes.Trim();
        ClosedAt = DateTimeOffset.UtcNow;
        Status = ShiftStatus.Closed;
    }
}
