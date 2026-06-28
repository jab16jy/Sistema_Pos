namespace POS.Application.DTOs.Products;

public sealed record CreateCategoryRequest(string Name);

public sealed record UpdateCategoryRequest(Guid CategoryId, string Name);

public sealed record CategoryResult(Guid Id, string Name, bool IsActive);
