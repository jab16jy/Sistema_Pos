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
        builder.Property(pi => pi.TotalCost)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("total_cost");
    }
}
