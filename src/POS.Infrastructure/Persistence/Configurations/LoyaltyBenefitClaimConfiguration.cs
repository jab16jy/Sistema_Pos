using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltyBenefitClaimConfiguration : IEntityTypeConfiguration<LoyaltyBenefitClaim>
{
    public void Configure(EntityTypeBuilder<LoyaltyBenefitClaim> builder)
    {
        builder.ToTable("LOYALTY_BENEFIT_CLAIMS");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasConversion<string>();
        builder.Property(c => c.LoyaltyBenefitId).HasConversion<string>();
        builder.Property(c => c.LoyaltyAccountId).HasConversion<string>();
        builder.Property(c => c.RedeemTransactionId).HasConversion<string>();
        builder.Property(c => c.ClaimedByUserId).HasConversion<string?>();
        builder.Property(c => c.DeliveredByUserId).HasConversion<string?>();
        builder.Property(c => c.CancelledByUserId).HasConversion<string?>();
        builder.Property(c => c.BenefitName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.PointsSpent).IsRequired();
        builder.Property(c => c.Status).HasConversion<int>().IsRequired();
        builder.Property(c => c.CancellationReason).HasMaxLength(300);

        builder.HasIndex(c => c.LoyaltyAccountId);

        // One claim per redeem movement: the pair is what cancellation compensates from, so a second
        // claim pointing at the same movement would let the same points be restored twice.
        builder.HasIndex(c => c.RedeemTransactionId).IsUnique();

        builder.HasOne<LoyaltyAccount>()
               .WithMany()
               .HasForeignKey(c => c.LoyaltyAccountId)
               .OnDelete(DeleteBehavior.Cascade);

        // Restrict, not Cascade: a claimed benefit must stay in the catalogue so the customer's
        // history keeps resolving. DeleteBenefitAsync deactivates instead once claims exist.
        builder.HasOne<LoyaltyBenefit>()
               .WithMany()
               .HasForeignKey(c => c.LoyaltyBenefitId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LoyaltyTransaction>()
               .WithMany()
               .HasForeignKey(c => c.RedeemTransactionId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}
