using POS.Domain.Entities;
using POS.Domain.Interfaces;

namespace POS.Application.UseCases.Products;

public sealed class DeleteProductUseCase
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProductUseCase(IProductRepository products, IUnitOfWork unitOfWork)
    {
        _products = products;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        Product product = await _products.GetByIdAsync(productId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Product was not found.");

        product.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
