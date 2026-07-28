using POS.Domain.Enums;

namespace POS.Domain.Entities;

public sealed class LoyaltyTier
{
    private LoyaltyTier()
    {
        Name = string.Empty;
    }

    public LoyaltyTier(string name, decimal minPoints, decimal discountPercent = 0, decimal pointsMultiplier = 1.0m, string color = "#64748B", LoyaltyTierTextForeground textForeground = LoyaltyTierTextForeground.White, string? benefits = null, bool allowsRedemption = false)
    {
        Id = Guid.NewGuid();
        Update(name, minPoints, discountPercent, pointsMultiplier, color, textForeground, benefits, allowsRedemption);
    }

    /// <summary>Maximum stored length of the free-text benefit list.</summary>
    private const int MaxBenefitsLength = 1000;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal MinPoints { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public decimal PointsMultiplier { get; private set; }
    public string Color { get; private set; } = "#64748B";
    public LoyaltyTierTextForeground TextForeground { get; private set; } = LoyaltyTierTextForeground.White;

    /// <summary>
    /// Free-text benefits unlocked at this tier, one per line (e.g. "Acceso a capacitaciones").
    /// Descriptive only — the calculated benefits stay in <see cref="DiscountPercent"/> and
    /// <see cref="PointsMultiplier"/>.
    /// </summary>
    // ponytail: newline-separated text, not a child table — nothing consumes a benefit
    // programmatically. Promote to LOYALTY_TIER_BENEFITS if a benefit ever needs its own state
    // (redeemed, delivered, expiry, usage count).
    public string Benefits { get; private set; } = string.Empty;

    /// <summary>
    /// When false, point redemption is not offered at checkout for customers on this tier.
    /// Redemption is a benefit the business opts into per tier, not the program's default behaviour.
    /// </summary>
    public bool AllowsRedemption { get; private set; }

    /// <summary>Benefit lines, already trimmed and free of blanks.</summary>
    public IReadOnlyList<string> BenefitLines => SplitBenefits(Benefits);

    /// <summary>Splits a stored benefits blob into display lines. Safe on null/empty.</summary>
    public static IReadOnlyList<string> SplitBenefits(string? benefits) => string.IsNullOrWhiteSpace(benefits)
        ? []
        : benefits.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Update(decimal minPoints, decimal discountPercent, decimal pointsMultiplier)
        => Update(Name, minPoints, discountPercent, pointsMultiplier, Color, TextForeground, Benefits, AllowsRedemption);

    public void Update(string name, decimal minPoints, decimal discountPercent, decimal pointsMultiplier, string color = "#64748B", LoyaltyTierTextForeground textForeground = LoyaltyTierTextForeground.White, string? benefits = null, bool allowsRedemption = false)
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

        // Normalised on write so every reader (UI list, PDF, thermal receipt) can split on '\n'
        // without re-trimming or filtering blank lines.
        var normalisedBenefits = string.Join('\n', SplitBenefits(benefits?.Replace("\r\n", "\n").Replace('\r', '\n')));
        if (normalisedBenefits.Length > MaxBenefitsLength)
            throw new ArgumentException($"Tier benefits cannot exceed {MaxBenefitsLength} characters.", nameof(benefits));

        Name = name.Trim();
        MinPoints = minPoints;
        DiscountPercent = discountPercent;
        PointsMultiplier = pointsMultiplier;
        Color = color;
        TextForeground = textForeground;
        Benefits = normalisedBenefits;
        AllowsRedemption = allowsRedemption;
    }
}
