namespace POS.Domain.Entities;

public sealed class LoyaltyTier
{
    private LoyaltyTier()
    {
        Name = string.Empty;
    }

    public LoyaltyTier(string name, decimal minPoints, decimal discountPercent = 0, decimal pointsMultiplier = 1.0m)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tier name is required.", nameof(name));

        Id = Guid.NewGuid();
        Name = name.Trim();
        MinPoints = minPoints;
        DiscountPercent = discountPercent;
        PointsMultiplier = pointsMultiplier;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public decimal MinPoints { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal PointsMultiplier { get; private set; }

    public void Update(decimal minPoints, decimal discountPercent, decimal pointsMultiplier)
    {
        MinPoints = minPoints;
        DiscountPercent = discountPercent;
        PointsMultiplier = pointsMultiplier;
    }
}
