namespace POS.Domain.Entities;

public sealed class Supplier
{
    private Supplier()
    {
        Name = string.Empty;
        TaxId = string.Empty;
    }

    public Supplier(string name, string taxId)
    {
        Id = Guid.NewGuid();
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Supplier name is required.", nameof(name)) : name.Trim();
        TaxId = string.IsNullOrWhiteSpace(taxId) ? throw new ArgumentException("Supplier tax id is required.", nameof(taxId)) : taxId.Trim();
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string TaxId { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }
}
