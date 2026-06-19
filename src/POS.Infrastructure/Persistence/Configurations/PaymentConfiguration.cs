using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("PAYMENTS");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion<string>();
        builder.Property(p => p.SaleId).HasConversion<string>();
        builder.Property(p => p.Method).HasConversion<int>();
        builder.Property(p => p.Amount)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("amount");
        builder.Property(p => p.ReceivedAmount)
               .HasConversion(
                   m => m.HasValue ? (decimal?)m.Value.Amount : null,
                   v => v.HasValue ? (Money?)new Money(v.Value) : null)
               .HasColumnName("received_amount");
        builder.Property(p => p.BankOrPlatform).HasMaxLength(100);
    }
}
