using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class ReceivableSettlementConfiguration : IEntityTypeConfiguration<ReceivableSettlement>
{
    public void Configure(EntityTypeBuilder<ReceivableSettlement> builder)
    {
        builder.ToTable("RECEIVABLE_SETTLEMENTS");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasConversion<string>();
        builder.Property(s => s.ReceivableId).HasConversion<string>().IsRequired();
        builder.Property(s => s.Amount)
               .HasConversion(m => m.Amount, v => new Money(v))
               .HasColumnName("amount");
        builder.Property(s => s.AppliedAt).IsRequired();
    }
}
