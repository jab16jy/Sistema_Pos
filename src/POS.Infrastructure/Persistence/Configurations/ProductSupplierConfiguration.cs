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
