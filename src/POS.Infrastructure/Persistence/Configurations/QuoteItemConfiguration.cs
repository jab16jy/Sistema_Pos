using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class QuoteItemConfiguration : IEntityTypeConfiguration<QuoteItem>
{
    public void Configure(EntityTypeBuilder<QuoteItem> builder)
    {
        builder.ToTable("QUOTE_ITEMS");
        builder.HasKey(qi => qi.Id);
        builder.Property(qi => qi.Id).HasConversion<string>();
        builder.Property(qi => qi.QuoteId).HasConversion<string>();
        builder.Property(qi => qi.ProductId).HasConversion<string>();
        builder.Property(qi => qi.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(qi => qi.UnitPrice)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("unit_price");
        builder.Property(qi => qi.Quantity)
               .HasConversion(q => q.Value, v => new Quantity(v))
               .HasColumnName("quantity");
        builder.Property(qi => qi.Discount).HasColumnName("discount");
    }
}
