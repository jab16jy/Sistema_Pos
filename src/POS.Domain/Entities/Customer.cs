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
        FullName = string.IsNullOrWhiteSpace(fullName) ? throw new ArgumentException("Customer name is required.", nameof(fullName)) : fullName.Trim();
        Identification = string.IsNullOrWhiteSpace(identification) ? throw new ArgumentException("Customer identification is required.", nameof(identification)) : identification.Trim();
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string FullName { get; private set; }
    public string Identification { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; }
}
