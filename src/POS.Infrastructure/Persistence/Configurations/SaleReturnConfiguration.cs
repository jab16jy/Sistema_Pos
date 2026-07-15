using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class SaleReturnConfiguration : IEntityTypeConfiguration<SaleReturn>
{
    public void Configure(EntityTypeBuilder<SaleReturn> builder)
    {
        builder.ToTable("SALE_RETURNS");
        builder.HasKey(saleReturn => saleReturn.Id);
        builder.Property(saleReturn => saleReturn.Id).HasConversion<string>();
        builder.Property(saleReturn => saleReturn.OriginalSaleId).HasConversion<string>();
        builder.Property(saleReturn => saleReturn.ProcessedByUserId).HasConversion<string>();
        builder.Property(saleReturn => saleReturn.Reason).IsRequired().HasMaxLength(500);
        builder.HasIndex(saleReturn => saleReturn.OriginalSaleId).IsUnique(false);

        builder.HasMany(r => r.Items)
               .WithOne()
               .HasForeignKey(i => i.SaleReturnId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
