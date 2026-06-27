using POS.Domain.Enums;

namespace POS.Application.DTOs.Invoices;

public sealed record SalesHistoryRequest(
    DateOnly? From,
    DateOnly? To,
    string? SearchText,
    int Page = 1,
    int PageSize = 50);

public sealed record SaleHistoryResult(
    Guid SaleId,
    string InvoiceNumber,
    Guid? CustomerId,
    string? CustomerName,
    SaleStatus Status,
    DateTimeOffset CreatedAt,
    decimal Total);

public sealed record ProcessReturnRequest(
    Guid SaleId,
    Guid ProcessedByUserId,
    string Reason);
