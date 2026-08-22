using POS.Application.DTOs.Products;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Products;

public sealed class UpdateProductUseCase
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISettingsRepository? _settings;

    public UpdateProductUseCase(
        IProductRepository products,
        ICategoryRepository categories,
        IUnitOfWork unitOfWork,
        ISettingsRepository? settings = null)
    {
        _products = products;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _settings = settings;
    }

    public async Task<ProductResult> ExecuteAsync(UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        Product product = await _products.GetByIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Product was not found.");

        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken).ConfigureAwait(false);

        ProductPresentationCountPolicy.Validate(
            request.Presentations?.Count ?? 0,
            await ProductPresentationCountPolicy.GetMaximumAsync(_settings, cancellationToken).ConfigureAwait(false));

        if (!string.IsNullOrWhiteSpace(request.Barcode))
        {
            Product? barcodeOwner = await _products.GetByBarcodeAsync(request.Barcode, cancellationToken).ConfigureAwait(false);
            if (barcodeOwner is not null && barcodeOwner.Id != product.Id)
            {
                throw new InvalidOperationException("Another product already uses this barcode.");
            }
        }

        product.UpdateDetails(
            request.Name,
            request.CategoryId,
            request.CostPrice.HasValue ? new Money(request.CostPrice.Value) : null,
            new Money(request.SalePrice),
            new StockQuantity(request.MinimumStock),
            request.Description,
            string.IsNullOrWhiteSpace(request.Barcode) ? null : new Barcode(request.Barcode),
            request.TaxRate,
            request.Presentations.ToDomain());

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToResult(product);
    }

    private static ProductResult ToResult(Product product) => new(
        product.Id,
        product.Name,
        product.CategoryId,
        product.CostPrice?.Amount,
        product.SalePrice.Amount,
        product.CurrentStock,
        product.MinimumStock,
        product.TaxRate,
        product.Description,
        product.Barcode,
        product.IsActive,
        product.ToDto());

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
