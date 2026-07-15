using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class CashClosingConfiguration : IEntityTypeConfiguration<CashClosing>
{
    public void Configure(EntityTypeBuilder<CashClosing> builder)
    {
        builder.ToTable("CASH_CLOSINGS");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion<string>();
        builder.Property(c => c.UserId).HasConversion<string>();

        // Legacy fields (retained for backward compatibility)
        builder.Property(c => c.ExpectedCash)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("expected_cash");
        builder.Property(c => c.CountedCash)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("counted_cash");
        builder.Property(c => c.DigitalPayments)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("digital_payments");

        // Separated auditable totals (RF-06)
        builder.Property(c => c.BilledTotal)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("billed_total")
               .HasDefaultValue(new Money(0m));
        builder.Property(c => c.CashCollected)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("cash_collected")
               .HasDefaultValue(new Money(0m));
        builder.Property(c => c.DigitalCollected)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("digital_collected")
               .HasDefaultValue(new Money(0m));
        builder.Property(c => c.ReceivableSettlementTotal)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("receivable_settlement_total")
               .HasDefaultValue(new Money(0m));
        builder.Property(c => c.ReceivableSettlementCash)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("receivable_settlement_cash")
               .HasDefaultValue(new Money(0m));
    }
}
