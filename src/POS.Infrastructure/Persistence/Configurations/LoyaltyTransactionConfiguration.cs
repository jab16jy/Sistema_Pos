using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltyTransactionConfiguration : IEntityTypeConfiguration<LoyaltyTransaction>
{
    public void Configure(EntityTypeBuilder<LoyaltyTransaction> builder)
    {
        builder.ToTable("LOYALTY_TRANSACTIONS");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasConversion<string>();
        builder.Property(t => t.LoyaltyAccountId).HasConversion<string>();
        builder.Property(t => t.Type).HasConversion<int>();
        builder.Property(t => t.SaleId).HasConversion<string?>();
        builder.Property(t => t.CompensatesTransactionId).HasConversion<string?>();
        builder.Property(t => t.SaleReturnId).HasConversion<string?>();
        builder.Property(t => t.Description).HasMaxLength(500);

        builder.HasIndex(t => t.LoyaltyAccountId);
        builder.HasIndex(t => new { t.CompensatesTransactionId, t.SaleReturnId })
               .IsUnique()
               .HasFilter("\"CompensatesTransactionId\" IS NOT NULL AND \"SaleReturnId\" IS NOT NULL");

        builder.HasOne<LoyaltyAccount>()
               .WithMany()
               .HasForeignKey(t => t.LoyaltyAccountId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<LoyaltyTransaction>()
               .WithMany()
               .HasForeignKey(t => t.CompensatesTransactionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
