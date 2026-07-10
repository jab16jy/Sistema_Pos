using POS.Domain.Interfaces;
using POS.Domain.ValueObjects;

namespace POS.Application.UseCases.Products;

/// <summary>Assigns one persistent internal Code 128 value to a product that has no provider barcode.</summary>
public sealed class AssignInternalBarcodeUseCase
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public AssignInternalBarcodeUseCase(IProductRepository products, IUnitOfWork unitOfWork)
    {
        _products = products;
        _unitOfWork = unitOfWork;
    }

    public async Task<string> ExecuteAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(productId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Product was not found.");

        if (!string.IsNullOrWhiteSpace(product.Barcode))
        {
            throw new InvalidOperationException("Product already has a barcode.");
        }

        string value = $"INT-{product.Id:N}";
        if (await _products.GetByBarcodeAsync(value, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new InvalidOperationException("Could not allocate a unique internal barcode.");
        }

        product.AssignBarcode(new Barcode(value));
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return value;
    }
}
