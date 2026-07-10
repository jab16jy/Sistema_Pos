namespace POS.Application.DTOs.Products;

public sealed record AssignProductsToCategoryRequest(Guid CategoryId, IReadOnlyCollection<Guid> ProductIds);
