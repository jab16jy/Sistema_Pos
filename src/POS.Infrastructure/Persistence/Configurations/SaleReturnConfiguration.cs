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
        builder.Property(saleReturn => saleReturn.OperationId).HasConversion<string>();
        builder.Property(saleReturn => saleReturn.Reason).IsRequired().HasMaxLength(500);
        builder.HasIndex(saleReturn => saleReturn.OriginalSaleId).IsUnique(false);
        builder.HasIndex(saleReturn => saleReturn.OperationId)
               .IsUnique()
               .HasFilter("\"OperationId\" IS NOT NULL");

        builder.HasOne<User>()
               .WithMany()
               .HasForeignKey(saleReturn => saleReturn.ProcessedByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Items)
               .WithOne()
               .HasForeignKey(i => i.SaleReturnId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
