using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;
using POS.Domain.ValueObjects;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class ReturnSettlementConfiguration : IEntityTypeConfiguration<ReturnSettlement>
{
    public void Configure(EntityTypeBuilder<ReturnSettlement> builder)
    {
        builder.ToTable("RETURN_SETTLEMENTS");
        builder.HasKey(settlement => settlement.Id);
        builder.Property(settlement => settlement.Id).HasConversion<string>();
        builder.Property(settlement => settlement.SaleReturnId).HasConversion<string>();
        builder.Property(settlement => settlement.Amount)
               .HasConversion(money => money.Amount, value => new Money(value));
        builder.Property(settlement => settlement.PaymentMethod).HasConversion<int>();
        builder.Property(settlement => settlement.SettledAt)
               .HasConversion(
                   value => value.ToString("yyyy-MM-dd HH:mm:ss.fffffffzzz"),
                   value => DateTimeOffset.Parse(value))
               .HasColumnType("TEXT");
        builder.HasIndex(settlement => settlement.SaleReturnId).IsUnique();
        builder.HasOne<SaleReturn>()
               .WithOne()
               .HasForeignKey<ReturnSettlement>(settlement => settlement.SaleReturnId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
