using POS.Application.DTOs.Products;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Products;

public sealed class UpdateProductUseCase
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProductUseCase(IProductRepository products, IUnitOfWork unitOfWork)
    {
        _products = products;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductResult> ExecuteAsync(UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        Product product = await _products.GetByIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Product was not found.");

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
            new Money(request.CostPrice),
            new Money(request.SalePrice),
            new StockQuantity(request.MinimumStock),
            request.Description,
            string.IsNullOrWhiteSpace(request.Barcode) ? null : new Barcode(request.Barcode));

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToResult(product);
    }

    private static ProductResult ToResult(Product product) => new(
        product.Id,
        product.Name,
        product.CategoryId,
        product.CostPrice.Amount,
        product.SalePrice.Amount,
        product.CurrentStock,
        product.MinimumStock,
        product.Description,
        product.Barcode,
        product.IsActive);
}
