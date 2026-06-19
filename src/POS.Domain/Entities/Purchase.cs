using POS.Domain.ValueObjects;

namespace POS.Domain.Entities;

public sealed class Purchase
{
    private readonly List<PurchaseItem> _items = [];

    private Purchase()
    {
    }

    public Purchase(Guid supplierId, Guid userId)
    {
        Id = Guid.NewGuid();
        SupplierId = supplierId;
        UserId = userId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<PurchaseItem> Items => _items.AsReadOnly();

    public void AddItem(Guid productId, Quantity quantity, Money totalCost)
    {
        _items.Add(new PurchaseItem(productId, quantity, totalCost));
    }
}
