using POS.Domain.Entities;

namespace POS.Application.DTOs.Customers;

public sealed record SaveCustomerRequest(
    Guid? CustomerId,
    string FullName,
    string Identification,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive);

public sealed record CustomerResult(
    Guid Id,
    string FullName,
    string Identification,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive)
{
    public static CustomerResult FromCustomer(Customer customer)
        => new(customer.Id, customer.FullName, customer.Identification, customer.Phone, customer.Email, customer.Address, customer.IsActive);
}

public sealed record CustomerSearchResult(
    Guid Id,
    string FullName,
    string Identification,
    string? Phone,
    string? Email,
    string? Address,
    bool IsActive)
{
    public static CustomerSearchResult FromCustomer(Customer customer)
        => new(customer.Id, customer.FullName, customer.Identification, customer.Phone, customer.Email, customer.Address, customer.IsActive);
}

public sealed record CustomerSearchRequest(string? SearchText, bool IncludeInactive);

public sealed record CustomerHistoryResult(
    Guid CustomerId,
    IReadOnlyList<CustomerHistoryEntry> Entries,
    decimal OutstandingBalance = 0m);

public sealed record CustomerHistoryEntry(
    DateTimeOffset OccurredAt,
    string Kind,
    string Reference,
    decimal Amount,
    string Status);
