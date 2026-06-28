namespace POS.Domain.Entities;

public sealed class Customer
{
    private Customer()
    {
        FullName = string.Empty;
        Identification = string.Empty;
    }

    public Customer(string fullName, string identification)
    {
        Id = Guid.NewGuid();
        FullName = NormalizeRequired(fullName, "Customer name is required.", nameof(fullName));
        Identification = NormalizeRequired(identification, "Customer identification is required.", nameof(identification));
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string FullName { get; private set; }
    public string Identification { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? LoyaltyTierId { get; private set; }

    public void SetLoyaltyTier(Guid? loyaltyTierId) => LoyaltyTierId = loyaltyTierId;

    public void UpdateContactInfo(string fullName, string identification, string? phone, string? email, string? address)
    {
        FullName = NormalizeRequired(fullName, "Customer name is required.", nameof(fullName));
        Identification = NormalizeRequired(identification, "Customer identification is required.", nameof(identification));
        Phone = NormalizeOptional(phone);
        Email = NormalizeOptional(email);
        Address = NormalizeOptional(address);
    }

    public void Reactivate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static string NormalizeRequired(string value, string message, string parameterName)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message, parameterName) : value.Trim();

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
