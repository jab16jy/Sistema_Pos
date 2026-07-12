using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class SupplierQuotationConfiguration : IEntityTypeConfiguration<SupplierQuotation>
{
    public void Configure(EntityTypeBuilder<SupplierQuotation> builder)
    {
        builder.ToTable("SUPPLIER_QUOTATIONS");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasConversion<string>();
        builder.Property(q => q.SupplierId).HasConversion<string>();
        builder.Property(q => q.PaymentTerms).HasMaxLength(200);
        builder.Property(q => q.Observations).HasMaxLength(1000);
        builder.HasIndex(q => new { q.SupplierId, q.EffectiveDate, q.Status });
        builder.HasMany(q => q.Items).WithOne().HasForeignKey(i => i.SupplierQuotationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SupplierQuotationItemConfiguration : IEntityTypeConfiguration<SupplierQuotationItem>
{
    public void Configure(EntityTypeBuilder<SupplierQuotationItem> builder)
    {
        builder.ToTable("SUPPLIER_QUOTATION_ITEMS");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasConversion<string>();
        builder.Property(i => i.SupplierQuotationId).HasConversion<string>();
        builder.Property(i => i.ProductId).HasConversion<string>();
        builder.Property(i => i.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(i => i.ProductReference).HasMaxLength(100);
        builder.Property(i => i.PurchaseUnitName).IsRequired().HasMaxLength(100);
        builder.Property(i => i.PurchaseUnitCost).HasConversion(m => m.Amount, v => new Money(v));
        builder.HasIndex(i => i.SupplierQuotationId);
    }
}
