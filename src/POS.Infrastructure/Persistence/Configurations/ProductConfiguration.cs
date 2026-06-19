using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("PRODUCTS");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion<string>();
        builder.Property(p => p.CategoryId).HasConversion<string>();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.Barcode).HasMaxLength(100);
        builder.Property(p => p.CostPrice)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("cost_price");
        builder.Property(p => p.SalePrice)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("sale_price");
    }
}
