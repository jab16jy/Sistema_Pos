using POS.Domain.Enums;

namespace POS.Domain.Entities;

public sealed class LoyaltyTier
{
    private LoyaltyTier()
    {
        Name = string.Empty;
    }

    public LoyaltyTier(string name, decimal minPoints, decimal discountPercent = 0, decimal pointsMultiplier = 1.0m, string color = "#64748B", LoyaltyTierTextForeground textForeground = LoyaltyTierTextForeground.White)
    {
        Id = Guid.NewGuid();
        Update(name, minPoints, discountPercent, pointsMultiplier, color, textForeground);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal MinPoints { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal PointsMultiplier { get; private set; }
    public string Color { get; private set; } = "#64748B";
    public LoyaltyTierTextForeground TextForeground { get; private set; } = LoyaltyTierTextForeground.White;

    public void Update(decimal minPoints, decimal discountPercent, decimal pointsMultiplier)
        => Update(Name, minPoints, discountPercent, pointsMultiplier, Color, TextForeground);

    public void Update(string name, decimal minPoints, decimal discountPercent, decimal pointsMultiplier, string color = "#64748B", LoyaltyTierTextForeground textForeground = LoyaltyTierTextForeground.White)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tier name is required.", nameof(name));

        if (minPoints < 0)
            throw new ArgumentOutOfRangeException(nameof(minPoints), "Tier minimum points cannot be negative.");

        if (discountPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(discountPercent), "Tier discount must be between 0 and 100.");

        if (pointsMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(pointsMultiplier), "Tier points multiplier must be greater than zero.");

        if (string.IsNullOrWhiteSpace(color) || !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$"))
            throw new ArgumentException("Tier color must be a hex value like #RRGGBB.", nameof(color));

        if (textForeground is not LoyaltyTierTextForeground.Black and not LoyaltyTierTextForeground.White)
            throw new ArgumentOutOfRangeException(nameof(textForeground), "Tier text foreground must be Black or White.");

        Name = name.Trim();
        MinPoints = minPoints;
        DiscountPercent = discountPercent;
        PointsMultiplier = pointsMultiplier;
        Color = color;
        TextForeground = textForeground;
    }
}
