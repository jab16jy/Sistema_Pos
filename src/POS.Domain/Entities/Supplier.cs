using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class Supplier
{
    private Supplier()
    {
        Name = string.Empty;
        TaxId = string.Empty;
    }

    public Supplier(string name, string taxId, string? phone = null, string? email = null)
    {
        Id = Guid.NewGuid();
        Name = string.Empty;
        TaxId = string.Empty;
        SetIdentity(name, taxId);
        Phone = ContactInfo.NormalizePhone(phone);
        Email = ContactInfo.NormalizeEmail(email);
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string TaxId { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }

    public void UpdateContactInfo(string name, string taxId, string? phone, string? email)
    {
        SetIdentity(name, taxId);
        Phone = ContactInfo.NormalizePhone(phone);
        Email = ContactInfo.NormalizeEmail(email);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private void SetIdentity(string name, string taxId)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Supplier name is required.", nameof(name)) : name.Trim();
        TaxId = string.IsNullOrWhiteSpace(taxId) ? throw new ArgumentException("Supplier tax id is required.", nameof(taxId)) : taxId.Trim();
    }

}
