using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class ReceivableConfiguration : IEntityTypeConfiguration<Receivable>
{
    public void Configure(EntityTypeBuilder<Receivable> builder)
    {
        builder.ToTable("RECEIVABLES");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion<string>();
        builder.Property(r => r.CustomerId).HasConversion<string>().IsRequired();
        builder.Property(r => r.SaleId).HasConversion<string>().IsRequired();
        builder.Property(r => r.OriginalBalance)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("original_balance");
        builder.Property(r => r.OutstandingBalance)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("outstanding_balance");
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.IsCancelled)
               .HasColumnName("is_cancelled")
               .HasDefaultValue(false)
               .IsRequired();
        builder.Property(r => r.Version)
               .HasColumnName("version")
               .HasDefaultValue(0)
               .IsConcurrencyToken()
               .IsRequired();

        builder.HasMany(r => r.Settlements)
               .WithOne()
               .HasForeignKey(s => s.ReceivableId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
