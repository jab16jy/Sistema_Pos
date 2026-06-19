using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.ToTable("SALES");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion<string>();
        builder.Property(s => s.CashierUserId).HasConversion<string>();
        builder.Property(s => s.CustomerId).HasConversion<string?>();
        builder.Property(s => s.InvoiceNumber).IsRequired().HasMaxLength(50);
        builder.Property(s => s.Status).HasConversion<int>();
        builder.HasMany(s => s.Items)
               .WithOne()
               .HasForeignKey(si => si.SaleId)
               .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Payments)
               .WithOne()
               .HasForeignKey(p => p.SaleId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
