using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class ProductSupplierConfiguration : IEntityTypeConfiguration<ProductSupplier>
{
    public void Configure(EntityTypeBuilder<ProductSupplier> builder)
    {
        builder.ToTable("PRODUCT_SUPPLIERS");
        builder.HasKey(ps => new { ps.ProductId, ps.SupplierId });
        builder.Property(ps => ps.ProductId).HasConversion<string>();
        builder.Property(ps => ps.SupplierId).HasConversion<string>();
        builder.Property(ps => ps.SupplierSku).HasMaxLength(100).HasColumnName("supplier_sku");
        builder.Property(ps => ps.PurchaseUnitName).HasMaxLength(100).HasColumnName("purchase_unit_name");
        builder.Property(ps => ps.UnitsPerPurchaseUnit).HasColumnName("units_per_purchase_unit");
        builder.Property(ps => ps.PurchaseUnitCost).HasColumnName("purchase_unit_cost");
        builder.Property(ps => ps.IsActive).HasColumnName("is_active");
        builder.HasIndex(ps => new { ps.SupplierId, ps.IsActive });
        builder.HasOne<Product>()
               .WithMany()
               .HasForeignKey(ps => ps.ProductId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Supplier>()
               .WithMany()
               .HasForeignKey(ps => ps.SupplierId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
