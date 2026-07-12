using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

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
        builder.Property(p => p.EffectiveDate).HasColumnName("effective_date");
        builder.Property(p => p.InvoiceNumber).HasMaxLength(100).HasColumnName("invoice_number");
        builder.Property(p => p.PaymentTerms).HasMaxLength(200).HasColumnName("payment_terms");
        builder.Property(p => p.PaymentMethod).HasMaxLength(100).HasColumnName("payment_method");
        builder.Property(p => p.Observations).HasMaxLength(1000).HasColumnName("observations");
        builder.Property(p => p.VatPolicy).HasColumnName("vat_policy");
        builder.Property(p => p.VatRate).HasColumnName("vat_rate");
        builder.Property(p => p.Subtotal).HasConversion(v => v.HasValue ? v.Value.Amount : (decimal?)null, v => v.HasValue ? new Money(v.Value) : null).HasColumnName("subtotal");
        builder.Property(p => p.VatAmount).HasConversion(v => v.HasValue ? v.Value.Amount : (decimal?)null, v => v.HasValue ? new Money(v.Value) : null).HasColumnName("vat_amount");
        builder.Property(p => p.Total).HasConversion(v => v.HasValue ? v.Value.Amount : (decimal?)null, v => v.HasValue ? new Money(v.Value) : null).HasColumnName("total");
        builder.HasIndex(p => new { p.SupplierId, p.EffectiveDate, p.InvoiceNumber });
        builder.HasMany(p => p.Items)
               .WithOne()
               .HasForeignKey(pi => pi.PurchaseId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
