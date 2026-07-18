using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class ReturnReceivableReconciliationConfiguration : IEntityTypeConfiguration<ReturnReceivableReconciliation>
{
    public void Configure(EntityTypeBuilder<ReturnReceivableReconciliation> builder)
    {
        builder.ToTable("RETURN_RECEIVABLE_RECONCILIATIONS");
        builder.HasKey(reconciliation => reconciliation.Id);
        builder.Property(reconciliation => reconciliation.Id).HasConversion<string>();
        builder.Property(reconciliation => reconciliation.SaleReturnId).HasConversion<string>();
        builder.Property(reconciliation => reconciliation.ReceivableId).HasConversion<string>();
        builder.Property(reconciliation => reconciliation.Amount)
               .HasConversion(money => money.Amount, value => new Money(value));
        builder.Property(reconciliation => reconciliation.ReconciledAt)
               .HasConversion(
                   value => value.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz"),
                   value => DateTimeOffset.Parse(value))
               .HasColumnType("TEXT");
        builder.HasIndex(reconciliation => reconciliation.SaleReturnId).IsUnique();
        builder.HasOne<SaleReturn>()
               .WithOne()
               .HasForeignKey<ReturnReceivableReconciliation>(reconciliation => reconciliation.SaleReturnId)
               .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Receivable>()
               .WithMany()
               .HasForeignKey(reconciliation => reconciliation.ReceivableId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
