namespace POS.Application.DTOs.Quotes;

public sealed record CreateQuoteRequest(
    Guid? CustomerId,
    string CustomerName,
    string? Phone,
    string? Email,
    int ValidityDays,
    string? Observations,
    IReadOnlyCollection<CreateQuoteItemRequest> Items);
