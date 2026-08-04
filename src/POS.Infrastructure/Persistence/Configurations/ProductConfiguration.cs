using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Enums;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("PRODUCTS");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion<string>();
        builder.Property(p => p.SourceCode).IsRequired().HasMaxLength(100);
        builder.Property(p => p.CategoryId).HasConversion<string>();
        builder.Property(p => p.SubCategoryId).HasConversion<string>();
        builder.Property(p => p.UnitOfMeasureId).HasConversion<string>();
        builder.Property(p => p.TaxProfileId).HasConversion<string>();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.Barcode).HasMaxLength(100);
        builder.Property(p => p.ItemType)
               .HasConversion(
                    value => value.ToString(),
                    value => Enum.Parse<CatalogItemType>(value))
               .HasMaxLength(20)
               .HasDefaultValue(CatalogItemType.Product);
        builder.Property(p => p.TracksInventory)
               .HasDefaultValue(true);
        builder.Property(p => p.CostPrice)
               .HasConversion(
                    value => value.HasValue ? value.Value.Amount : (decimal?)null,
                    value => value.HasValue ? new Money(value.Value) : null)
               .HasColumnName("cost_price");
        builder.Property(p => p.SalePrice)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("sale_price");
        builder.Property(p => p.UnitsPerBox)
               .HasColumnName("units_per_box");
        builder.Property(p => p.BoxPrice)
               .HasConversion(
                    value => value.HasValue ? value.Value.Amount : (decimal?)null,
                    value => value.HasValue ? new Money(value.Value) : null)
               .HasColumnName("box_price");
        builder.Property(p => p.HalfBoxUnits)
               .HasColumnName("half_box_units");
        builder.Property(p => p.HalfBoxPrice)
               .HasConversion(
                    value => value.HasValue ? value.Value.Amount : (decimal?)null,
                    value => value.HasValue ? new Money(value.Value) : null)
               .HasColumnName("half_box_price");
        builder.Property(p => p.QuarterBoxUnits)
               .HasColumnName("quarter_box_units");
        builder.Property(p => p.QuarterBoxPrice)
               .HasConversion(
                    value => value.HasValue ? value.Value.Amount : (decimal?)null,
                    value => value.HasValue ? new Money(value.Value) : null)
               .HasColumnName("quarter_box_price");
        builder.Property(p => p.TaxRate)
               .HasColumnName("tax_rate");
        builder.Property(p => p.Version)
               .IsConcurrencyToken()
               .HasDefaultValue(0);
        builder.HasOne<Category>()
               .WithMany()
               .HasForeignKey(p => p.CategoryId)
               .IsRequired()
               .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SubCategory>()
               .WithMany()
               .HasForeignKey(p => p.SubCategoryId)
               .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<UnitOfMeasure>()
               .WithMany()
               .HasForeignKey(p => p.UnitOfMeasureId)
               .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<TaxProfile>()
               .WithMany()
               .HasForeignKey(p => p.TaxProfileId)
               .OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(p => p.SourceCode).IsUnique();
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.Barcode)
               .IsUnique()
               .HasFilter("\"Barcode\" IS NOT NULL");
    }
}
