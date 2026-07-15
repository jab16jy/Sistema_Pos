using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class CashierShiftConfiguration : IEntityTypeConfiguration<CashierShift>
{
    public void Configure(EntityTypeBuilder<CashierShift> builder)
    {
        builder.ToTable("CASHIER_SHIFTS");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion<string>();
        builder.Property(s => s.CashierUserId).HasConversion<string>();
        builder.Property(s => s.CashierUsername).IsRequired().HasMaxLength(100);
        builder.Property(s => s.OpeningCash)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("opening_cash");
        builder.Property(s => s.ClosingCash)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("closing_cash");
        builder.Property(s => s.SalesTotal)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("sales_total")
               .HasDefaultValue(Money.Zero);
        builder.Property(s => s.CashPayments)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("cash_payments")
               .HasDefaultValue(Money.Zero);
        builder.Property(s => s.DigitalPayments)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("digital_payments")
               .HasDefaultValue(Money.Zero);
        builder.Property(s => s.ReceivableSettlementCash)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("receivable_settlement_cash")
               .HasDefaultValue(Money.Zero);
        builder.Property(s => s.ReceivableSettlementDigital)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("receivable_settlement_digital")
               .HasDefaultValue(Money.Zero);
        builder.Property(s => s.HasClosingSummary)
               .HasColumnName("has_closing_summary")
               .HasDefaultValue(false);
        builder.Property(s => s.Notes).HasMaxLength(500);
        builder.Property(s => s.Status).HasConversion<int>();
        builder.HasIndex(s => new { s.CashierUserId, s.Status });
        builder.HasIndex(s => s.OpenedAt);
    }
}
