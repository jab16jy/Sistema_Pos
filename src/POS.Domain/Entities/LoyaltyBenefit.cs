namespace POS.Domain.Entities;

/// <summary>
/// A reward the business offers in exchange for loyalty points (e.g. "Asesoría personalizada").
/// The point cost lives here, in the catalogue, so no cashier ever prices a reward by hand.
/// </summary>
public sealed class LoyaltyBenefit
{
    private LoyaltyBenefit()
    {
        Name = string.Empty;
        Description = string.Empty;
    }

    public LoyaltyBenefit(string name, int pointsCost, string? description = null)
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTimeOffset.UtcNow;
        IsActive = true;
        Update(name, pointsCost, description);
    }

    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 500;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    /// <summary>Points a customer must spend to claim this benefit. Always greater than zero.</summary>
    public int PointsCost { get; private set; }

    /// <summary>Inactive benefits stay in the catalogue for history but can no longer be claimed.</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void Update(string name, int pointsCost, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Benefit name is required.", nameof(name));
        }

        string trimmedName = name.Trim();
        if (trimmedName.Length > MaxNameLength)
        {
            throw new ArgumentException($"Benefit name cannot exceed {MaxNameLength} characters.", nameof(name));
        }

        if (pointsCost <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointsCost), "Benefit point cost must be greater than zero.");
        }

        string trimmedDescription = description?.Trim() ?? string.Empty;
        if (trimmedDescription.Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"Benefit description cannot exceed {MaxDescriptionLength} characters.", nameof(description));
        }

        Name = trimmedName;
        PointsCost = pointsCost;
        Description = trimmedDescription;
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;
}
