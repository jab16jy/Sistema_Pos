namespace POS.Domain.Entities;

public sealed class UnitOfMeasure
{
    private UnitOfMeasure()
    {
        Name = string.Empty;
        Abbreviation = string.Empty;
    }

    public UnitOfMeasure(string name, string abbreviation)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Unit of measure name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(abbreviation))
            throw new ArgumentException("Abbreviation is required.", nameof(abbreviation));

        Id = Guid.NewGuid();
        Name = name.Trim();
        Abbreviation = abbreviation.Trim();
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Abbreviation { get; private set; }
    public bool IsActive { get; private set; }

    public void Deactivate() => IsActive = false;
}
