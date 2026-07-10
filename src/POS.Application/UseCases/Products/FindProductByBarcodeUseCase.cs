using POS.Application.DTOs.Products;
using POS.Application.Interfaces;

namespace POS.Application.UseCases.Products;

public sealed class FindProductByBarcodeUseCase
{
    private readonly IProductLookupService _productLookupService;

    public FindProductByBarcodeUseCase(IProductLookupService productLookupService)
    {
        _productLookupService = productLookupService ?? throw new ArgumentNullException(nameof(productLookupService));
    }

    public Task<ProductLookupResult?> ExecuteAsync(string barcode, CancellationToken cancellationToken = default)
        => _productLookupService.FindByBarcodeAsync(barcode, cancellationToken);
}
