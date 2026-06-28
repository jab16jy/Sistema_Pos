using POS.Application.DTOs.Products;
using POS.Domain.Entities;
using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Products;

public sealed class CreateProductUseCase
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProductUseCase(IProductRepository products, IUnitOfWork unitOfWork)
    {
        _products = products;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> ExecuteAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Barcode) &&
            await _products.GetByBarcodeAsync(request.Barcode, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException("Another product already uses this barcode.");
        }

        var product = new Product(
            request.Name,
            request.CategoryId,
            new Money(request.CostPrice),
            new Money(request.SalePrice),
            new StockQuantity(request.CurrentStock),
            new StockQuantity(request.MinimumStock),
            request.Description,
            string.IsNullOrWhiteSpace(request.Barcode) ? null : new Barcode(request.Barcode),
            request.TaxRate);

        await _products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return product.Id;
    }
}
