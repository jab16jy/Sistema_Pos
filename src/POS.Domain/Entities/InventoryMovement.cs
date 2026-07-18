using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class InventoryMovement
{
    private InventoryMovement()
    {
        Reason = string.Empty;
    }

    public InventoryMovement(Guid productId, InventoryMovementType type, Quantity quantity, string reason, Guid userId, Guid? referenceId = null)
    {
        Id = Guid.NewGuid();
        ProductId = productId;
        Type = type;
        Quantity = quantity.Value;
        Reason = string.IsNullOrWhiteSpace(reason) ? throw new ArgumentException("Movement reason is required.", nameof(reason)) : reason.Trim();
        UserId = userId;
        ReferenceId = referenceId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public InventoryMovementType Type { get; private set; }
    public decimal Quantity { get; private set; }
    public string Reason { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public void UpdateReason(string newReason)
    {
        Reason = string.IsNullOrWhiteSpace(newReason)
            ? throw new ArgumentException("Movement reason is required.", nameof(newReason))
            : newReason.Trim();
    }
}
