using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("PURCHASES");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion<string>();
        builder.Property(p => p.SupplierId).HasConversion<string>();
        builder.Property(p => p.UserId).HasConversion<string>();
        builder.HasMany(p => p.Items)
               .WithOne()
               .HasForeignKey(pi => pi.PurchaseId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
