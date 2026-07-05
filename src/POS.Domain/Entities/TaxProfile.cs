namespace POS.Domain.Entities;

public sealed class TaxProfile
{
    private TaxProfile()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public TaxProfile(string code, string name, decimal rate)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Tax profile code is required.", nameof(code));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tax profile name is required.", nameof(name));

        if (rate < 0m || rate > 100m)
            throw new ArgumentOutOfRangeException(nameof(rate), "Tax profile rate must be between 0 and 100.");

        Id = Guid.NewGuid();
        Code = code.Trim().ToUpperInvariant();
        Name = name.Trim();
        Rate = rate;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public decimal Rate { get; private set; }
    public bool IsActive { get; private set; }
}
