namespace POS.Application.DTOs.Products;

public sealed record CreateCategoryRequest(string Name, bool IsActive = true);

public sealed record UpdateCategoryRequest(Guid CategoryId, string Name, bool IsActive);

public sealed record CategoryResult(Guid Id, string Name, bool IsActive);
