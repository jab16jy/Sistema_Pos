using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class ProductPresentationConfiguration : IEntityTypeConfiguration<ProductPresentation>
{
    public void Configure(EntityTypeBuilder<ProductPresentation> builder)
    {
        builder.ToTable("PRODUCT_PRESENTATIONS");
        builder.HasKey(pp => pp.Id);
        builder.Property(pp => pp.Id).HasConversion<string>();
        builder.Property(pp => pp.ProductId).HasConversion<string>().HasColumnName("product_id");
        // NOCASE so the unique index below rejects "caja" next to "Caja", matching the
        // OrdinalIgnoreCase rule the domain enforces.
        builder.Property(pp => pp.Name).IsRequired().HasMaxLength(100).HasColumnName("name").UseCollation("NOCASE");
        builder.Property(pp => pp.Units).HasColumnName("units");
        builder.Property(pp => pp.Price)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("price");
        builder.Property(pp => pp.DisplayOrder).HasColumnName("display_order");

        // The domain rejects duplicate names per product; the index makes the database agree,
        // so a concurrent save cannot leave a product with two presentations of the same name.
        builder.HasIndex(pp => new { pp.ProductId, pp.Name }).IsUnique();
    }
}
