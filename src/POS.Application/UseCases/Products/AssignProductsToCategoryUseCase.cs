using POS.Application.DTOs.Products;
using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Products;

public sealed class AssignProductsToCategoryUseCase
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public AssignProductsToCategoryUseCase(
        IProductRepository products,
        ICategoryRepository categories,
        IUnitOfWork unitOfWork)
    {
        _products = products;
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> ExecuteAsync(
        AssignProductsToCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CategoryId == Guid.Empty)
        {
            throw new ArgumentException("Category is required.", nameof(request));
        }

        if (request.ProductIds is null || request.ProductIds.Count == 0)
        {
            throw new ArgumentException("Select at least one product.", nameof(request));
        }

        if (await _categories.GetByIdAsync(request.CategoryId, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new InvalidOperationException("Selected category was not found.");
        }

        List<Guid> productIds = request.ProductIds.Distinct().ToList();
        var products = new List<Product>(productIds.Count);

        foreach (Guid productId in productIds)
        {
            Product product = await _products.GetByIdAsync(productId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("One or more selected products were not found.");
            products.Add(product);
        }

        List<Product> productsToUpdate = products
            .Where(product => product.CategoryId != request.CategoryId)
            .ToList();

        foreach (Product product in productsToUpdate)
        {
            product.AssignCategory(request.CategoryId);
        }

        if (productsToUpdate.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return productsToUpdate.Count;
    }
}
