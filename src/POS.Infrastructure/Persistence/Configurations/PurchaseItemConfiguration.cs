using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        builder.ToTable("PURCHASE_ITEMS");
        builder.HasKey(pi => pi.Id);
        builder.Property(pi => pi.Id).HasConversion<string>();
        builder.Property(pi => pi.PurchaseId).HasConversion<string>();
        builder.Property(pi => pi.ProductId).HasConversion<string>();
        builder.Property(pi => pi.TotalCost).HasColumnName("total_cost");
        builder.Property(pi => pi.ProductName).HasMaxLength(200).HasColumnName("product_name");
        builder.Property(pi => pi.ProductReference).HasMaxLength(100).HasColumnName("product_reference");
        builder.Property(pi => pi.PurchaseUnitName).HasMaxLength(100).HasColumnName("purchase_unit_name");
        builder.Property(pi => pi.UnitsPerPurchaseUnit).HasColumnName("units_per_purchase_unit");
        builder.Property(pi => pi.InventoryUnitsReceived).HasColumnName("inventory_units_received");
        builder.Property(pi => pi.PurchaseUnitCost).HasConversion(v => v.HasValue ? v.Value.Amount : (decimal?)null, v => v.HasValue ? new Money(v.Value) : null).HasColumnName("purchase_unit_cost");
        builder.Property(pi => pi.BaseUnitCost).HasConversion(v => v.HasValue ? v.Value.Amount : (decimal?)null, v => v.HasValue ? new Money(v.Value) : null).HasColumnName("base_unit_cost");
        builder.Property(pi => pi.Subtotal).HasConversion(v => v.HasValue ? v.Value.Amount : (decimal?)null, v => v.HasValue ? new Money(v.Value) : null).HasColumnName("subtotal");
    }
}
