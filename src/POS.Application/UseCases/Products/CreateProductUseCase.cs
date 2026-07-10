using POS.Application.DTOs.Products;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Products;

public sealed class CreateProductUseCase
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductUseCase(IProductRepository products, ICategoryRepository categories, IUnitOfWork unitOfWork)
    {
        _products = products;
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(request.Barcode) &&
            await _products.GetByBarcodeAsync(request.Barcode, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException("Another product already uses this barcode.");
        }

        var product = new Product(
            request.Name,
            request.CategoryId,
            request.CostPrice.HasValue ? new Money(request.CostPrice.Value) : null,
            new Money(request.SalePrice),
            new StockQuantity(request.CurrentStock),
            new StockQuantity(request.MinimumStock),
            request.Description,
            string.IsNullOrWhiteSpace(request.Barcode) ? null : new Barcode(request.Barcode),
            request.TaxRate,
            request.UnitsPerBox,
            request.BoxPrice.HasValue ? new Money(request.BoxPrice.Value) : null);

        await _products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return product.Id;
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("Category is required.", nameof(categoryId));
        }

        if (await _categories.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new InvalidOperationException("Selected category was not found.");
        }
    }
}
