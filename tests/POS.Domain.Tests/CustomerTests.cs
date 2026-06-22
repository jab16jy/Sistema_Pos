using POS.Domain.Entities;
using Xunit;

namespace POS.Domain.Tests;

public sealed class CustomerTests
{
    [Fact]
    public void UpdateContactInfo_WithValidIdentity_KeepsCustomerSearchableAndActive()
    {
        var customer = new Customer(" Ana Gomez ", " 123 ");

        customer.UpdateContactInfo("Ana Maria Gomez", "123", "3001234567", "ana@example.com", "Main Street 1");

        Assert.Equal("Ana Maria Gomez", customer.FullName);
        Assert.Equal("123", customer.Identification);
        Assert.Equal("3001234567", customer.Phone);
        Assert.Equal("ana@example.com", customer.Email);
        Assert.Equal("Main Street 1", customer.Address);
        Assert.True(customer.IsActive);
    }

    [Fact]
    public void UpdateContactInfo_WithoutRequiredIdentity_RejectsRecoverableValidation()
    {
        var customer = new Customer("Ana Gomez", "123");

        var exception = Assert.Throws<ArgumentException>(() =>
            customer.UpdateContactInfo("", "123", null, null, null));

        Assert.Contains("Customer name is required", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Ana Gomez", customer.FullName);
    }

    [Fact]
    public void Deactivate_MarksCustomerUnavailableWithoutRemovingIdentity()
    {
        var customer = new Customer("Ana Gomez", "123");

        customer.Deactivate();

        Assert.False(customer.IsActive);
        Assert.Equal("Ana Gomez", customer.FullName);
        Assert.Equal("123", customer.Identification);
    }
}
