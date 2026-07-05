using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class ProductImportBatchConfiguration : IEntityTypeConfiguration<ProductImportBatch>
{
    public void Configure(EntityTypeBuilder<ProductImportBatch> builder)
    {
        builder.ToTable("PRODUCT_IMPORT_BATCHES");
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.Id).HasConversion<string>();
        builder.Property(batch => batch.SourceFileName).IsRequired().HasMaxLength(260);
        builder.Property(batch => batch.ImportedAt).IsRequired();
        builder.HasIndex(batch => batch.SourceFileName).IsUnique();
    }
}
