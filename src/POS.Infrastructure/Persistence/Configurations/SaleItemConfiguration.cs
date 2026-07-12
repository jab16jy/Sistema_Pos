using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class SaleItemConfiguration : IEntityTypeConfiguration<SaleItem>
{
    public void Configure(EntityTypeBuilder<SaleItem> builder)
    {
        builder.ToTable("SALE_ITEMS");
        builder.HasKey(si => si.Id);
        builder.Property(si => si.Id).HasConversion<string>();
        builder.Property(si => si.SaleId).HasConversion<string>();
        builder.Property(si => si.ProductId).HasConversion<string>();
        builder.Property(si => si.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(si => si.UnitPrice)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("unit_price");
        builder.Property(si => si.Discount)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("discount");
        builder.Property(si => si.TaxRate)
               .HasColumnName("tax_rate")
               .HasDefaultValue(19m);
        builder.Property(si => si.Presentation)
               .HasConversion<int>()
               .HasColumnName("presentation")
               .HasDefaultValue(ProductPresentation.Unit);
        builder.Property(si => si.UnitsPerPresentation)
               .HasColumnName("units_per_presentation")
               .HasDefaultValue(1);

        builder.Property(si => si.UnitCost)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("UnitCost");
    }
}
