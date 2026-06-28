namespace POS.Domain.Entities;

public sealed class ProductSupplier
{
    private ProductSupplier()
    {
    }

    public ProductSupplier(Guid productId, Guid supplierId)
    {
        ProductId = productId;
        SupplierId = supplierId;
    }

    public Guid ProductId { get; private set; }
    public Guid SupplierId { get; private set; }
}
