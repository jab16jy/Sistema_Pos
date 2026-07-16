using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class SaleReturnItemConfiguration : IEntityTypeConfiguration<SaleReturnItem>
{
    public void Configure(EntityTypeBuilder<SaleReturnItem> builder)
    {
        builder.ToTable("SALE_RETURN_ITEMS");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasConversion<string>();
        builder.Property(i => i.SaleReturnId).HasConversion<string>();
        builder.Property(i => i.SaleItemId).HasConversion<string>().HasDefaultValue(Guid.Empty);
        builder.Property(i => i.ProductId).HasConversion<string>();
        builder.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(i => i.UnitPrice)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("UnitPrice");
        builder.Property(i => i.RefundAmount)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("RefundAmount");
    }
}
