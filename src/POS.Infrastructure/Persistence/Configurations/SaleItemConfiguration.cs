using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
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

        // Phase 2: UnitCost is computed in-memory and ignored by EF until Phase 3 creates the column+migration.
        // Task 3.10 will remove this Ignore and add the proper column mapping + migration.
        builder.Ignore(si => si.UnitCost);
    }
}
