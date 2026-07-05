using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class ProductImportLineConfiguration : IEntityTypeConfiguration<ProductImportLine>
{
    public void Configure(EntityTypeBuilder<ProductImportLine> builder)
    {
        builder.ToTable("PRODUCT_IMPORT_LINES");
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Id).HasConversion<string>();
        builder.Property(line => line.BatchId).HasConversion<string>();
        builder.Property(line => line.ImportedProductId).HasConversion<string>();
        builder.Property(line => line.SourceType).IsRequired().HasMaxLength(40);
        builder.Property(line => line.SourceCode).IsRequired().HasMaxLength(100);
        builder.Property(line => line.SourceName).IsRequired().HasMaxLength(200);
        builder.Property(line => line.SourceUnit).HasMaxLength(100);
        builder.Property(line => line.SourcePrice).IsRequired().HasMaxLength(80);
        builder.Property(line => line.SourceTax).IsRequired().HasMaxLength(80);
        builder.Property(line => line.SourceStock).IsRequired().HasMaxLength(80);
        builder.Property(line => line.Status).IsRequired().HasMaxLength(40);
        builder.Property(line => line.Notes).HasMaxLength(500);
        builder.HasOne<ProductImportBatch>()
               .WithMany()
               .HasForeignKey(line => line.BatchId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Product>()
               .WithMany()
               .HasForeignKey(line => line.ImportedProductId)
               .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(line => new { line.BatchId, line.RowNumber }).IsUnique();
        builder.HasIndex(line => line.SourceCode);
    }
}
