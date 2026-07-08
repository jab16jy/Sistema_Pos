using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;
using Xunit;

namespace POS.Domain.Tests;

public sealed class SupplierPurchaseTests
{
    [Fact]
    public void Supplier_UpdateAndDeactivate_KeepSupplierSelectableStateExplicit()
    {
        var supplier = new Supplier("Acme Foods", "900123", "+57 300 111 2233", "sales@acme.test");

        supplier.UpdateContactInfo("Acme Wholesale", "900456", "+57 301 222 3344", null);
        supplier.Deactivate();

        Assert.Equal("Acme Wholesale", supplier.Name);
        Assert.Equal("900456", supplier.TaxId);
        Assert.Equal("+57 301 222 3344", supplier.Phone);
        Assert.Null(supplier.Email);
        Assert.False(supplier.IsActive);
    }

    [Fact]
    public void Purchase_AddItem_DerivesAuditableItemTotals()
    {
        var purchase = new Purchase(Guid.NewGuid(), Guid.NewGuid());
        Guid productId = Guid.NewGuid();

        purchase.AddItem(productId, new Quantity(3m), new Money(12_000m));

        PurchaseItem item = Assert.Single(purchase.Items);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal(3m, item.Quantity);
        Assert.Equal(12_000m, item.TotalCost.Amount);
    }

    [Fact]
    public void InventoryMovement_WithPurchaseReference_CarriesStockTraceability()
    {
        Guid purchaseId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();

        var movement = new InventoryMovement(productId, InventoryMovementType.Entry, new Quantity(7m), "Purchase registration", Guid.NewGuid(), purchaseId);

        Assert.Equal(productId, movement.ProductId);
        Assert.Equal(7m, movement.Quantity);
        Assert.Equal(purchaseId, movement.ReferenceId);
        Assert.Equal(InventoryMovementType.Entry, movement.Type);
    }
}
