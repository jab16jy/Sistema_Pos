using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Configurations;

internal sealed class LoyaltyRuleConfiguration : IEntityTypeConfiguration<LoyaltyRule>
{
    public void Configure(EntityTypeBuilder<LoyaltyRule> builder)
    {
        builder.ToTable("LOYALTY_RULES");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion<string>();
        builder.Property(r => r.PointsPerCurrencyUnit).HasColumnType("TEXT");
        builder.Property(r => r.RedemptionValuePerPoint).HasColumnType("TEXT");
        builder.Property(r => r.Description).HasMaxLength(500);
    }
}
